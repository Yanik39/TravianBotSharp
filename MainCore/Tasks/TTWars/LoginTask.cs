﻿using FluentResults;
using MainCore.Errors;
using OpenQA.Selenium;
using System.Linq;
using System.Threading;

namespace MainCore.Tasks.TTWars
{
    public class LoginTask : Base.LoginTask
    {
        public LoginTask(int accountId, CancellationToken cancellationToken = default) : base(accountId, cancellationToken)
        {
        }

        protected override Result Login()
        {
            var html = _chromeBrowser.GetHtml();

            var usernameNode = _systemPageParser.GetUsernameNode(html);

            var passwordNode = _systemPageParser.GetPasswordNode(html);

            var buttonNode = _systemPageParser.GetLoginButton(html);
            if (buttonNode is null)
            {
                _logManager.Information(AccountId, "Account is already logged in. Skip login task");
                return Result.Ok();
            }

            if (usernameNode is null)
            {
                return Result.Fail(new Retry("Cannot find username box"));
            }

            if (passwordNode is null)
            {
                return Result.Fail(new Retry("Cannot find password box"));
            }

            using var context = _contextFactory.CreateDbContext();
            var account = context.Accounts.Find(AccountId);
            var access = context.Accesses.Where(x => x.AccountId == AccountId).OrderByDescending(x => x.LastUsed).FirstOrDefault();
            var chrome = _chromeBrowser.GetChrome();

            var usernameElement = chrome.FindElements(By.XPath(usernameNode.XPath));
            if (usernameElement.Count == 0)
            {
                return Result.Fail(new Retry("Cannot find username box"));
            }
            var passwordElement = chrome.FindElements(By.XPath(passwordNode.XPath));
            if (passwordElement.Count == 0)
            {
                return Result.Fail(new Retry("Cannot find password box"));
            }
            var buttonElements = chrome.FindElements(By.XPath(buttonNode.XPath));
            if (buttonElements.Count == 0)
            {
                return Result.Fail(new Retry("Cannot find login button"));
            }

            usernameElement[0].SendKeys(Keys.Home);
            usernameElement[0].SendKeys(Keys.Shift + Keys.End);
            usernameElement[0].SendKeys(account.Username);

            passwordElement[0].SendKeys(Keys.Home);
            passwordElement[0].SendKeys(Keys.Shift + Keys.End);
            passwordElement[0].SendKeys(access.Password);

            buttonElements[0].Click();

            html = _chromeBrowser.GetHtml();
            if (_checkHelper.IsSkipTutorial(html))
            {
                var skipButton = html.DocumentNode.Descendants().FirstOrDefault(x => x.HasClass("questButtonSkipTutorial"));
                if (skipButton is null)
                {
                    return Result.Fail(new Retry("Cannot find skip quest button"));
                }
                var skipButtons = chrome.FindElements(By.XPath(skipButton.XPath));
                if (skipButtons.Count == 0)
                {
                    return Result.Fail(new Retry("Cannot find skip quest button"));
                }
                var result = _navigateHelper.Click(AccountId, skipButtons[0]);
                if (result.IsFailed) return result.WithError(new Trace(Trace.TraceMessage()));
            }

            return Result.Ok();
        }
    }
}