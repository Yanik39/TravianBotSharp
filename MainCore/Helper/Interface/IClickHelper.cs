﻿using FluentResults;

namespace MainCore.Helper.Interface
{
    public interface IClickHelper
    {
        Result ClickCompleteNow(int accountId);

        Result ClickStartAdventure(int accountId, int x, int y);

        Result ClickStartFarm(int accountId, int farmId);
    }
}