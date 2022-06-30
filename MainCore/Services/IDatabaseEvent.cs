﻿using System;

namespace MainCore.Services
{
    public interface IDatabaseEvent
    {
        public event Action AccountsTableUpdate;

        public void OnAccountsTableUpdate();
    }
}