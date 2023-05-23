﻿using FluentResults;
using MainCore.Errors;
using MainCore.Helper.Interface;
using MainCore.Tasks.Base;
using Splat;
using System;
using System.Threading;

namespace MainCore.Tasks.FunctionTasks
{
    public sealed class SleepTask : AccountBotTask
    {
        private readonly ISleepHelper _sleepHelper;

        public SleepTask(int accountId, CancellationToken cancellationToken = default) : base(accountId, cancellationToken)
        {
            _sleepHelper = Locator.Current.GetService<ISleepHelper>();
        }

        public override Result Execute()
        {
            _sleepHelper.Load(AccountId, CancellationToken);

            var result = _sleepHelper.Execute();
            if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            ExecuteAt = DateTime.Now.Add(_sleepHelper.GetWorkTime());
            return Result.Ok();
        }
    }
}