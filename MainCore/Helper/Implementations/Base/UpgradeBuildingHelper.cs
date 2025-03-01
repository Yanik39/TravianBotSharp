﻿using FluentResults;
using HtmlAgilityPack;
using MainCore.Enums;
using MainCore.Errors;
using MainCore.Helper.Interface;
using MainCore.Models.Database;
using MainCore.Models.Runtime;
using MainCore.Parser.Interface;
using MainCore.Services.Interface;
using Microsoft.EntityFrameworkCore;
using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MainCore.Helper.Implementations.Base
{
    public class UpgradeBuildingHelper : IUpgradeBuildingHelper
    {
        protected readonly IDbContextFactory<AppDbContext> _contextFactory;
        protected readonly IPlanManager _planManager;
        protected readonly IChromeManager _chromeManager;
        protected readonly ISystemPageParser _systemPageParser;
        protected readonly IBuildingsHelper _buildingsHelper;
        protected readonly INavigateHelper _navigateHelper;
        protected readonly ILogManager _logManager;

        public UpgradeBuildingHelper(IDbContextFactory<AppDbContext> contextFactory, IPlanManager planManager, IChromeManager chromeManager, ISystemPageParser systemPageParser, IBuildingsHelper buildingsHelper, INavigateHelper navigateHelper, ILogManager logManager)
        {
            _contextFactory = contextFactory;
            _planManager = planManager;
            _chromeManager = chromeManager;
            _systemPageParser = systemPageParser;
            _buildingsHelper = buildingsHelper;
            _navigateHelper = navigateHelper;
            _logManager = logManager;
        }

        public PlanTask ExtractResField(int villageId, PlanTask buildingTask)
        {
            List<VillageBuilding> buildings = null; // Potential buildings to be upgraded next
            using var context = _contextFactory.CreateDbContext();
            switch (buildingTask.ResourceType)
            {
                case ResTypeEnums.AllResources:
                    buildings = context.VillagesBuildings.Where(x => x.VillageId == villageId).Where(x => x.Type == BuildingEnums.Woodcutter || x.Type == BuildingEnums.ClayPit || x.Type == BuildingEnums.IronMine || x.Type == BuildingEnums.Cropland).ToList();
                    break;

                case ResTypeEnums.ExcludeCrop:
                    buildings = context.VillagesBuildings.Where(x => x.VillageId == villageId).Where(x => x.Type == BuildingEnums.Woodcutter || x.Type == BuildingEnums.ClayPit || x.Type == BuildingEnums.IronMine).ToList();
                    break;

                case ResTypeEnums.OnlyCrop:
                    buildings = context.VillagesBuildings.Where(x => x.VillageId == villageId).Where(x => x.Type == BuildingEnums.Cropland).ToList();
                    break;
            }

            foreach (var b in buildings)
            {
                if (b.IsUnderConstruction)
                {
                    var levelUpgrading = context.VillagesCurrentlyBuildings.Where(x => x.VillageId == villageId).Count(x => x.Location == b.Id);
                    b.Level += (byte)levelUpgrading;
                }
            }
            buildings = buildings.Where(b => b.Level < buildingTask.Level).ToList();

            if (buildings.Count == 0) return null;

            var building = buildings.OrderBy(x => x.Level).FirstOrDefault();

            return new()
            {
                Type = PlanTypeEnums.General,
                Level = building.Level + 1,
                Building = building.Type,
                Location = building.Id,
            };
        }

        public void RemoveFinishedCB(int villageId)
        {
            using var context = _contextFactory.CreateDbContext();
            var tasksDone = context.VillagesCurrentlyBuildings.Where(x => x.VillageId == villageId && x.CompleteTime <= DateTime.Now);

            if (!tasksDone.Any()) return;

            foreach (var taskDone in tasksDone)
            {
                var building = context.VillagesBuildings.Find(villageId, taskDone.Location);
                if (building is null)
                {
                    building = context.VillagesBuildings.FirstOrDefault(x => x.VillageId == villageId && x.Type == taskDone.Type);
                    if (building is null) continue;
                }

                if (building.Level < taskDone.Level) building.Level = taskDone.Level;

                taskDone.Type = 0;
                taskDone.Level = -1;
                taskDone.CompleteTime = DateTime.MaxValue;
            }
            context.SaveChanges();
        }

        public PlanTask GetFirstResTask(int villageId)
        {
            var tasks = _planManager.GetList(villageId);
            var task = tasks.FirstOrDefault(x => x.Type == PlanTypeEnums.ResFields || x.Building.IsResourceField());
            return task;
        }

        public PlanTask GetFirstBuildingTask(int villageId)
        {
            var tasks = _planManager.GetList(villageId);
            var infrastructureTasks = tasks.Where(x => x.Type == PlanTypeEnums.General && !x.Building.IsResourceField());
            var task = infrastructureTasks.FirstOrDefault(x => IsInfrastructureTaskVaild(villageId, x));
            return task;
        }

        public PlanTask GetFirstTask(int villageId)
        {
            var tasks = _planManager.GetList(villageId);
            foreach (var task in tasks)
            {
                if (task.Type != PlanTypeEnums.General) return task;
                if (task.Building.IsResourceField()) return task;
                if (IsInfrastructureTaskVaild(villageId, task)) return task;
            }
            return null;
        }

        private bool IsInfrastructureTaskVaild(int villageId, PlanTask planTask)
        {
            (_, var prerequisiteBuildings) = planTask.Building.GetPrerequisiteBuildings();
            using var context = _contextFactory.CreateDbContext();
            var buildings = context.VillagesBuildings.Where(x => x.VillageId == villageId).ToList();
            foreach (var prerequisiteBuilding in prerequisiteBuildings)
            {
                var building = buildings.OrderByDescending(x => x.Level).FirstOrDefault(x => x.Type == prerequisiteBuilding.Building);
                if (building is null) return false;
                if (building.Level < prerequisiteBuilding.Level) return false;
            }
            return true;
        }

        public Result<bool> IsNeedAdsUpgrade(int accountId, int villageId, PlanTask buildingTask)
        {
            using var context = _contextFactory.CreateDbContext();
            var setting = context.VillagesSettings.Find(villageId);
            if (!setting.IsAdsUpgrade) return false;

            var building = context.VillagesBuildings.Find(villageId, buildingTask.Location);

            if (buildingTask.Building.IsResourceField() && building.Level == 0) return false;
            if (buildingTask.Building.IsNotAdsUpgrade()) return false;

            var chromeBrowser = _chromeManager.Get(accountId);
            var html = chromeBrowser.GetHtml();
            var container = html.DocumentNode.Descendants("div").FirstOrDefault(x => x.HasClass("upgradeButtonsContainer"));

            var durationNode = container.Descendants("div").FirstOrDefault(x => x.HasClass("duration"));
            if (durationNode is null)
            {
                return Result.Fail(new Retry("Cannot found duration in build page. (div)"));
            }
            var dur = durationNode.Descendants("span").FirstOrDefault(x => x.HasClass("value"));
            if (dur is null)
            {
                return Result.Fail(new Retry("Cannot found duration in build page. (span)"));
            }
            var duration = dur.InnerText.ToDuration();
            if (setting.AdsUpgradeTime > duration.TotalMinutes) return false;
            return true;
        }

        private int[] GetResourceNeed(int accountId, BuildingEnums building, bool multiple = false)
        {
            var chromeBrowser = _chromeManager.Get(accountId);
            var html = chromeBrowser.GetHtml();

            HtmlNode contractNode;
            if (multiple && !building.IsResourceField())
            {
                contractNode = html.GetElementbyId($"contract_building{(int)building}");
            }
            else
            {
                contractNode = _systemPageParser.GetContractNode(html);
            }
            var resWrapper = contractNode.Descendants("div").FirstOrDefault(x => x.HasClass("resourceWrapper"));
            var resNodes = resWrapper.ChildNodes.Where(x => x.HasClass("resource") || x.HasClass("resources")).ToList();
            var resNeed = new int[4];
            for (var i = 0; i < 4; i++)
            {
                var node = resNodes[i];
                var strResult = new string(node.InnerText.Where(c => char.IsDigit(c)).ToArray());
                if (string.IsNullOrEmpty(strResult)) resNeed[i] = 0;
                else resNeed[i] = int.Parse(strResult);
            }
            return resNeed;
        }

        public bool IsEnoughResource(int accountId, int villageId, BuildingEnums building, bool isNewBuilding)
        {
            var resNeed = GetResourceNeed(accountId, building, isNewBuilding);
            using var context = _contextFactory.CreateDbContext();
            var resCurrent = context.VillagesResources.Find(villageId);
            return resCurrent.Wood > resNeed[0] && resCurrent.Clay > resNeed[1] && resCurrent.Iron > resNeed[2] && resCurrent.Crop > resNeed[3];
        }

        public long[] GetResourceMissing(int accountId, int villageId, BuildingEnums building, bool isNewBuilding)
        {
            var resNeed = GetResourceNeed(accountId, building, isNewBuilding);
            using var context = _contextFactory.CreateDbContext();
            var resCurrent = context.VillagesResources.Find(villageId);
            return new long[] { resNeed[0] - resCurrent.Wood, resNeed[1] - resCurrent.Clay, resNeed[2] - resCurrent.Iron, resNeed[3] - resCurrent.Crop };
        }

        public bool IsEnoughFreeCrop(int villageId, BuildingEnums building)
        {
            using var context = _contextFactory.CreateDbContext();
            var freecrop = context.VillagesResources.Find(villageId).FreeCrop;
            return freecrop > 5 || building == BuildingEnums.Cropland;
        }

        public Result<bool> GotoBuilding(int accountId, int villageId, PlanTask buildingTask)
        {
            {
                var result = _navigateHelper.GoToBuilding(accountId, buildingTask.Location);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }

            using var context = _contextFactory.CreateDbContext();
            var building = context.VillagesBuildings.Find(villageId, buildingTask.Location);

            bool isNewBuilding = false;
            if (building.Type == BuildingEnums.Site)
            {
                isNewBuilding = true;
                var tab = buildingTask.Building.GetBuildingsCategory();
                {
                    var result = _navigateHelper.SwitchTab(accountId, tab);
                    if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
                }
            }
            else
            {
                if (building.Level == -1)
                {
                    isNewBuilding = true;
                }
                else
                {
                    if (buildingTask.Building.HasMultipleTabs() && building.Level != 0)
                    {
                        var result = _navigateHelper.SwitchTab(accountId, 0);
                        if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
                    }
                }
            }
            return isNewBuilding;
        }

        public Result Construct(int accountId, PlanTask buildingTask)
        {
            var chromeBrowser = _chromeManager.Get(accountId);

            var html = chromeBrowser.GetHtml();
            var node = html.GetElementbyId($"contract_building{(int)buildingTask.Building}");
            if (node is null)
            {
                return Result.Fail(new Retry("Cannot find building box"));
            }
            var button = node.Descendants("button").FirstOrDefault(x => x.HasClass("new"));

            // Check for prerequisites
            if (button is null)
            {
                return Result.Fail(new Retry($"Cannot find Build button for {buildingTask.Building}"));
            }

            var chrome = chromeBrowser.GetChrome();
            var elements = chrome.FindElements(By.XPath(button.XPath));
            if (elements.Count == 0)
            {
                return Result.Fail(new Retry($"Cannot find Build button for {buildingTask.Building}"));
            }

            {
                var result = _navigateHelper.Click(accountId, elements[0]);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }
            return Result.Ok();
        }

        public Result Upgrade(int accountId, PlanTask buildingTask)
        {
            var chromeBrowser = _chromeManager.Get(accountId);
            var html = chromeBrowser.GetHtml();
            var container = html.DocumentNode.Descendants("div").FirstOrDefault(x => x.HasClass("upgradeButtonsContainer"));
            if (container is null)
            {
                return Result.Fail(new Retry("Cannot find upgrading box"));
            }
            var upgradeButton = container.Descendants("button").FirstOrDefault(x => x.HasClass("build"));

            if (upgradeButton == null)
            {
                return Result.Fail(new Retry($"Cannot find upgrade button for {buildingTask.Building}"));
            }

            var chrome = chromeBrowser.GetChrome();

            var elements = chrome.FindElements(By.XPath(upgradeButton.XPath));
            if (elements.Count == 0)
            {
                return Result.Fail(new Retry($"Cannot find upgrade button for {buildingTask.Building}"));
            }

            {
                var result = _navigateHelper.Click(accountId, elements[0]);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }
            return Result.Ok();
        }

        private Result UpgradeAds_UpgradeClicking(int accountId, PlanTask buildingTask)
        {
            var chromeBrowser = _chromeManager.Get(accountId);
            var html = chromeBrowser.GetHtml();

            var nodeFastUpgrade = html.DocumentNode.Descendants("button").FirstOrDefault(x => x.HasClass("videoFeatureButton") && x.HasClass("green"));
            if (nodeFastUpgrade is null)
            {
                return Result.Fail(new Retry($"Cannot find fast upgrade button for {buildingTask.Building}"));
            }
            var chrome = chromeBrowser.GetChrome();
            var elements = chrome.FindElements(By.XPath(nodeFastUpgrade.XPath));
            if (elements.Count == 0)
            {
                return Result.Fail(new Retry($"Cannot find fast upgrade button for {buildingTask.Building}"));
            }
            {
                var result = _navigateHelper.Click(accountId, elements[0]);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }
            return Result.Ok();
        }

        private Result UpgradeAds_AccpetAds(int accountId)
        {
            var chromeBrowser = _chromeManager.Get(accountId);
            var html = chromeBrowser.GetHtml();
            var nodeNotShowAgainConfirm = html.DocumentNode.SelectSingleNode("//input[@name='adSalesVideoInfoScreen']");
            if (nodeNotShowAgainConfirm is not null)
            {
                var chrome = chromeBrowser.GetChrome();
                var elements = chrome.FindElements(By.XPath(nodeNotShowAgainConfirm.ParentNode.XPath));
                if (elements.Count == 0)
                {
                    return Result.Fail(new Retry("Cannot find accept watching ads button"));
                }
                {
                    var result = _navigateHelper.Click(accountId, elements[0]);
                    if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
                }
                chrome.ExecuteScript("jQuery(window).trigger('showVideoWindowAfterInfoScreen')");
            }
            return Result.Ok();
        }

        private void UpgradeAds_CloseOtherTab(int accountId)
        {
            var chromeBrowser = _chromeManager.Get(accountId);
            var chrome = chromeBrowser.GetChrome();
            var current = chrome.CurrentWindowHandle;
            while (chrome.WindowHandles.Count > 1)
            {
                var others = chrome.WindowHandles.FirstOrDefault(x => !x.Equals(current));
                chrome.SwitchTo().Window(others);
                chrome.Close();
                chrome.SwitchTo().Window(current);
            }
        }

        private Result UpgradeAds_ClickPlayAds(int accountId, PlanTask buildingTask)
        {
            var chromeBrowser = _chromeManager.Get(accountId);
            var html = chromeBrowser.GetHtml();
            var nodeIframe = html.GetElementbyId("videoFeature");
            if (nodeIframe is null)
            {
                return Result.Fail(new Retry($"Cannot find iframe for {buildingTask.Building}"));
            }
            var chrome = chromeBrowser.GetChrome();
            var elementsIframe = chrome.FindElements(By.XPath(nodeIframe.XPath));
            if (elementsIframe.Count == 0)
            {
                return Result.Fail(new Retry($"Cannot find iframe for {buildingTask.Building}"));
            }
            {
                var result = _navigateHelper.Click(accountId, elementsIframe[0]);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }

            chrome.SwitchTo().DefaultContent();

            Thread.Sleep(Random.Shared.Next(1300, 2000));
            // close if bot click on playing ads
            // chrome will open new tab & pause ads
            do
            {
                var handles = chrome.WindowHandles;
                if (handles.Count == 1) break;

                var current = chrome.CurrentWindowHandle;
                var other = chrome.WindowHandles.FirstOrDefault(x => !x.Equals(current));
                chrome.SwitchTo().Window(other);
                chrome.Close();
                chrome.SwitchTo().Window(current);

                {
                    var result = _navigateHelper.Click(accountId, elementsIframe[0]);
                    if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
                }
                chrome.SwitchTo().DefaultContent();
            }
            while (true);
            return Result.Ok();
        }

        private Result UpgradeAds_DontShowThis(int accountId)
        {
            var chromeBrowser = _chromeManager.Get(accountId);
            var html = chromeBrowser.GetHtml();
            if (html.GetElementbyId("dontShowThisAgain") is not null)
            {
                var chrome = chromeBrowser.GetChrome();
                var dontshowthisagain = chrome.FindElements(By.Id("dontShowThisAgain"));
                if (dontshowthisagain.Count == 0)
                {
                    return Result.Fail(new Retry("Cannot find dont show this agian button"));
                }

                {
                    var result = _navigateHelper.Click(accountId, dontshowthisagain[0]);
                    if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
                }
                Thread.Sleep(800);
                {
                    var dialogbuttonok = chrome.FindElements(By.ClassName("dialogButtonOk"));
                    var result = _navigateHelper.Click(accountId, dialogbuttonok[0]);
                    if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
                }
            }
            return Result.Ok();
        }

        public Result UpgradeAds(int accountId, PlanTask buildingTask)
        {
            {
                var result = UpgradeAds_UpgradeClicking(accountId, buildingTask);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }

            Thread.Sleep(Random.Shared.Next(2400, 5300));

            {
                var result = UpgradeAds_AccpetAds(accountId);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }

            Thread.Sleep(Random.Shared.Next(20000, 25000));

            UpgradeAds_CloseOtherTab(accountId);

            {
                var result = UpgradeAds_ClickPlayAds(accountId, buildingTask);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }
            {
                var result = _navigateHelper.WaitPageChanged(accountId, "dorf");
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }
            {
                var result = UpgradeAds_DontShowThis(accountId);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }
            return Result.Ok();
        }
    }
}