﻿using MainCore.Enums;
using MainCore.Models.Runtime;
using MainCore.Services.Interface;
using MainCore.Tasks;
using MainCore.Tasks.TravianOfficial;
using System.Collections.Generic;
using System.Threading;

namespace MainCore.Services.Implementations.TaskFactories
{
    public class TravianOfficialTaskFactory : ITaskFactory
    {
        public BotTask GetImproveTroopsTask(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new ImproveTroopsTask(villageId, accountId, cancellationToken);
        }

        public BotTask GetInstantUpgradeTask(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.InstantUpgrade(villageId, accountId, cancellationToken);
        }

        public BotTask GetLoginTask(int accountId, CancellationToken cancellationToken = default)
        {
            return new LoginTask(accountId, cancellationToken);
        }

        public BotTask GetNPCTask(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new NPCTask(villageId, accountId, cancellationToken);
        }

        public BotTask GetNPCTask(int villageId, int accountId, Resources ratio, CancellationToken cancellationToken = default)
        {
            return new NPCTask(villageId, accountId, ratio, cancellationToken);
        }

        public BotTask GetRefreshVillageTask(int villageId, int accountId, int mode = 0, CancellationToken cancellationToken = default)
        {
            return new RefreshVillage(villageId, accountId, mode, cancellationToken);
        }

        public BotTask GetSleepTask(int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.SleepTask(accountId, cancellationToken);
        }

        public BotTask GetStartAdventureTask(int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.StartAdventure(accountId, cancellationToken);
        }

        public BotTask GetStartFarmListTask(int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.StartFarmList(accountId, cancellationToken);
        }

        public BotTask GetUpdateAdventuresTask(int accountId, CancellationToken cancellationToken = default)
        {
            return new UpdateAdventures(accountId, cancellationToken);
        }

        public BotTask GetUpdateBothDorfTask(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UpdateBothDorf(villageId, accountId, cancellationToken);
        }

        public BotTask GetUpdateDorf1Task(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UpdateDorf1(villageId, accountId, cancellationToken);
        }

        public BotTask GetUpdateDorf2Task(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UpdateDorf2(villageId, accountId, cancellationToken);
        }

        public BotTask GetUpdateFarmListTask(int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UpdateFarmList(accountId, cancellationToken);
        }

        public BotTask GetUpdateHeroItemsTask(int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UpdateHeroItems(accountId, cancellationToken);
        }

        public BotTask GetUpdateInfoTask(int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UpdateInfo(accountId, cancellationToken);
        }

        public BotTask GetUpdateTroopLevelTask(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UpdateTroopLevel(villageId, accountId, cancellationToken);
        }

        public BotTask GetUpdateVillageTask(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UpdateVillage(villageId, accountId, cancellationToken);
        }

        public BotTask GetUpgradeBuildingTask(int villageId, int accountId, CancellationToken cancellationToken = default)
        {
            return new UpgradeBuilding(villageId, accountId, cancellationToken);
        }

        public BotTask GetUseHeroResourcesTask(int villageId, int accountId, List<(HeroItemEnums, int)> items, CancellationToken cancellationToken = default)
        {
            return new Tasks.Base.UseHeroResources(villageId, accountId, items, cancellationToken);
        }
    }
}