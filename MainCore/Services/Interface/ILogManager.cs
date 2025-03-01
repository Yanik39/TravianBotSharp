﻿using MainCore.Models.Runtime;
using MainCore.Tasks;
using System;
using System.Collections.Generic;

namespace MainCore.Services.Interface
{
    public interface ILogManager
    {
        public void Init();

        public void AddAccount(int accountId);

        public LinkedList<LogMessage> GetLog(int accountId);

        public void Information(int accountId, string message);

        public void Information(int accountId, string message, BotTask task);

        public void Warning(int accountId, string message);

        public void Warning(int accountId, string message, BotTask task);

        public void Error(int accountId, string message, Exception error = null);

        public void Shutdown();
    }
}