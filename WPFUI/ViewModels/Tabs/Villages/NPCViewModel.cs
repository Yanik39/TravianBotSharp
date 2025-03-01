﻿using MainCore.Models.Database;
using ReactiveUI;
using System;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Windows;
using WPFUI.Models;
using WPFUI.ViewModels.Abstract;

namespace WPFUI.ViewModels.Tabs.Villages
{
    public class NPCViewModel : VillageTabBaseViewModel
    {
        public NPCViewModel()
        {
            RefreshCommand = ReactiveCommand.Create(RefreshTask);
            NPCCommand = ReactiveCommand.Create(NPCTask);
        }

        protected override void Init(int villageId)
        {
            LoadData(villageId);
        }

        private void LoadData(int villageId)
        {
            using var context = _contextFactory.CreateDbContext();
            var resources = context.VillagesResources.Find(villageId);
            var updateTime = context.VillagesUpdateTime.Find(villageId);
            var setting = context.VillagesSettings.Find(VillageId);

            var dorf1 = updateTime.Dorf1;
            var dorf2 = updateTime.Dorf2;

            RxApp.MainThreadScheduler.Schedule(() =>
            {
                Resources = resources;
                LastUpdate = dorf1 > dorf2 ? dorf1 : dorf2;

                Ratio.Wood = setting.AutoNPCWood.ToString();
                Ratio.Clay = setting.AutoNPCClay.ToString();
                Ratio.Iron = setting.AutoNPCIron.ToString();
                Ratio.Crop = setting.AutoNPCCrop.ToString();
            });
        }

        private void RefreshTask()
        {
            _taskManager.Add(AccountId, _taskFactory.GetRefreshVillageTask(VillageId, AccountId));
            MessageBox.Show("Added Refresh resources task to queue");
        }

        private void NPCTask()
        {
            _taskManager.Add(AccountId, _taskFactory.GetNPCTask(VillageId, AccountId, Ratio.GetResources()));
            MessageBox.Show("Added NPC task to queue");
        }

        private VillageResources _resources;

        public VillageResources Resources
        {
            get => _resources;
            set => this.RaiseAndSetIfChanged(ref _resources, value);
        }

        private Resources _ratio = new();

        public Resources Ratio
        {
            get => _ratio;
            set => this.RaiseAndSetIfChanged(ref _ratio, value);
        }

        private DateTime _lastUpdate;

        public DateTime LastUpdate
        {
            get => _lastUpdate;
            set => this.RaiseAndSetIfChanged(ref _lastUpdate, value);
        }

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; set; }
        public ReactiveCommand<Unit, Unit> NPCCommand { get; set; }
    }
}