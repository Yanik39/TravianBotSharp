﻿using DynamicData;
using DynamicData.Kernel;
using MainCore.Tasks.Base;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Linq;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using System.Windows;
using WPFUI.Models;
using WPFUI.ViewModels.Abstract;

namespace WPFUI.ViewModels.Tabs
{
    public class FarmingViewModel : AccountTabBaseViewModel
    {
        public ReactiveCommand<Unit, Unit> StartCommand { get; }
        public ReactiveCommand<Unit, Unit> StopCommand { get; }
        public ReactiveCommand<Unit, Unit> SaveCommand { get; }
        public ReactiveCommand<ListBoxItem, Unit> LoadCommand { get; }
        public ReactiveCommand<Unit, Unit> ActiveCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }

        public FarmingViewModel()
        {
            StartCommand = ReactiveCommand.CreateFromTask(StartTask);
            StopCommand = ReactiveCommand.CreateFromTask(StopTask);

            SaveCommand = ReactiveCommand.CreateFromTask(SaveData);
            LoadCommand = ReactiveCommand.Create<ListBoxItem>(LoadFarm);

            ActiveCommand = ReactiveCommand.CreateFromTask(ActiveTask, this.WhenAnyValue(vm => vm.CurrentFarm).Select(x => x is not null));
            RefreshCommand = ReactiveCommand.CreateFromTask(RefreshTask);

            this.WhenAnyValue(vm => vm.CurrentFarm).BindTo(_selectorViewModel, vm => vm.Farm);
            this.WhenAnyValue(vm => vm.CurrentFarm).InvokeCommand(LoadCommand);
            _eventManager.FarmListUpdate += OnFarmListUpdate;
        }

        private async Task StartTask()
        {
            await Task.Run(() =>
            {
                var accountId = AccountId;
                using var context = _contextFactory.CreateDbContext();
                var farms = context.Farms.Where(x => x.AccountId == accountId).ToList();
                var activeFarms = farms.Where(x =>
                {
                    var farmSetting = context.FarmsSettings.Find(x.Id);
                    return farmSetting.IsActive;
                }).ToList();
                if (activeFarms.Count == 0) return;
                var tasks = _taskManager.GetList(AccountId);
                var task = tasks.OfType<StartFarmList>().FirstOrDefault();
                if (task is null)
                {
                    _taskManager.Add(accountId, new StartFarmList(accountId));
                }
                else
                {
                    task.ExecuteAt = DateTime.Now;
                    _taskManager.Update(AccountId);
                }
            });

            MessageBox.Show("Send all active farm");
        }

        private async Task StopTask()
        {
            await Task.Run(() =>
            {
                var accountId = AccountId;
                var tasks = _taskManager.GetList(accountId);
                var task = tasks.OfType<StartFarmList>().FirstOrDefault();
                if (task is null) return;

                _taskManager.Remove(accountId, task);
            });

            MessageBox.Show("Removed send farm task from queue");
        }

        public async Task SaveData()
        {
            if (CurrentFarm is null) return;
            _waitingWindow.Show("Saving ...");
            await Task.Run(() =>
            {
                using var context = _contextFactory.CreateDbContext();
                var settings = context.AccountsSettings.Find(_selectorViewModel.Account.Id);
                settings.FarmIntervalMin = Interval - DiffInterval;
                if (settings.FarmIntervalMin < 0) settings.FarmIntervalMin = 0;
                settings.FarmIntervalMax = Interval + DiffInterval;
                context.Update(settings);
                context.SaveChanges();
            });
            _waitingWindow.Close();
            MessageBox.Show("Saved");
        }

        public void LoadFarm(ListBoxItem farm)
        {
            if (farm is null)
            {
                ContentButton = "=====================";
            }
            else
            {
                using var context = _contextFactory.CreateDbContext();
                var setting = context.FarmsSettings.Find(farm.Id);
                ContentButton = setting.IsActive ? "Deactive" : "Active";
            }
        }

        public async Task ActiveTask()
        {
            if (CurrentFarm is null) return;
            _waitingWindow.Show("Processing ...");
            var active = true;
            await Task.Run(() =>
            {
                using var context = _contextFactory.CreateDbContext();
                var setting = context.FarmsSettings.Find(CurrentFarm.Id);
                setting.IsActive = !setting.IsActive;
                active = setting.IsActive;
                context.Update(setting);
                context.SaveChanges();
            });
            _waitingWindow.Close();
            MessageBox.Show("Done");
            CurrentFarm.Color = active ? Color.ForestGreen.ToMediaColor() : Color.Red.ToMediaColor();
            ContentButton = active ? "Deactive" : "Active";
        }

        private Task RefreshTask()
        {
            var accountId = _selectorViewModel.Account.Id;
            var tasks = _taskManager.GetList(accountId).OfType<UpdateFarmList>();
            if (!tasks.Any())
            {
                _taskManager.Add(accountId, new UpdateFarmList(accountId));
            }
            else
            {
                var updateFarmList = tasks.First();
                updateFarmList.ExecuteAt = DateTime.Now;
                _taskManager.Update(accountId);
            }
            return Task.CompletedTask;
        }

        private void OnFarmListUpdate(int accountId)
        {
            if (!IsActive) return;
            if (AccountId != accountId) return;

            LoadData(accountId);
        }

        private void LoadData(int id)
        {
            using var context = _contextFactory.CreateDbContext();
            var farms = context.Farms
                .Where(x => x.AccountId == id)
                .AsList()
                .Select(farm =>
                {
                    var farmSetting = context.FarmsSettings.Find(farm.Id);
                    var color = farmSetting.IsActive ? Color.ForestGreen.ToMediaColor() : Color.Red.ToMediaColor();
                    return new ListBoxItem(farm.Id, farm.Name, color);
                }).ToList();

            var settings = context.AccountsSettings.Find(_selectorViewModel.Account.Id);

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                FarmList.Clear();
                FarmList.AddRange(farms);
                if (farms.Any())
                {
                    CurrentFarm = FarmList[0];
                }
                else
                {
                    CurrentFarm = null;
                }

                Interval = (settings.FarmIntervalMax + settings.FarmIntervalMin) / 2;
                DiffInterval = (settings.FarmIntervalMax - settings.FarmIntervalMin) / 2;
            });
        }

        protected override void Init(int accountId)
        {
            LoadData(accountId);
        }

        private int _interval;

        public int Interval
        {
            get => _interval;
            set => this.RaiseAndSetIfChanged(ref _interval, value);
        }

        private int _diffInterval;

        public int DiffInterval
        {
            get => _diffInterval;
            set => this.RaiseAndSetIfChanged(ref _diffInterval, value);
        }

        private string _contentButton;

        public string ContentButton
        {
            get => _contentButton;
            set => this.RaiseAndSetIfChanged(ref _contentButton, value);
        }

        public ObservableCollection<ListBoxItem> FarmList { get; } = new();
        private ListBoxItem _currentFarm;

        public ListBoxItem CurrentFarm
        {
            get => _currentFarm;
            set => this.RaiseAndSetIfChanged(ref _currentFarm, value);
        }
    }
}