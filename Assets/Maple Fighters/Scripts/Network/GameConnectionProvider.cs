﻿using System;
using System.Threading.Tasks;
using Authorization.Client.Common;
using CommonCommunicationInterfaces;
using CommonTools.Coroutines;
using CommonTools.Log;
using Scripts.Containers;
using Scripts.UI.Core;
using Scripts.UI.Windows;
using Scripts.Utils;

namespace Scripts.Services
{
    public class GameConnectionProvider : ServiceConnectionProviderBase<GameConnectionProvider>
    {
        private Action onAuthorized;
        private AuthorizationStatus authorizationStatus = AuthorizationStatus.Failed;
        private string serverName;

        public void Connect(string serverName, Action onAuthorized, PeerConnectionInformation peerConnectionInformation)
        {
            this.serverName = serverName;
            this.onAuthorized = onAuthorized;

            var serverConnectionInformation = GetServerConnectionInformation(ServerType.Game, peerConnectionInformation);
            CoroutinesExecutor.StartTask((yield) => Connect(yield, serverConnectionInformation));
        }

        protected override void OnPreConnection()
        {
            var noticeWindow = UserInterfaceContainer.Instance.Add<NoticeWindow>().AssertNotNull();
            noticeWindow.Message.text = $"Connecting to the {serverName} server... Please wait.";
        }

        protected override void OnConnectionFailed()
        {
            Action onButtonClicked = delegate 
            {
                // TODO: Do it via controller which will update the list.

                var gameServerSelectorWindow = UserInterfaceContainer.Instance.Add<GameServerSelectorWindow>().AssertNotNull();
                gameServerSelectorWindow.Show();
            };

            var noticeWindow = UserInterfaceContainer.Instance.Get<NoticeWindow>().AssertNotNull();
            noticeWindow.Message.text = $"Could not connect to the {serverName} server.";
            noticeWindow.OkButton.interactable = true;
            noticeWindow.OkButtonClickedAction = onButtonClicked;
        }

        protected override void OnConnectionEstablished()
        {
            CoroutinesExecutor.StartTask(Authorize);
        }

        protected override void OnDisconnected(DisconnectReason reason, string details)
        {
            base.OnDisconnected(reason, details);

            GoBackToLogin();
        }

        private void GoBackToLogin()
        {
            if (authorizationStatus == AuthorizationStatus.Succeed)
            {
                LoadedObjects.DestroyAll();
            }
            else
            {
                OnNonAuthorized();
            }
        }

        protected override Task<AuthorizeResponseParameters> Authorize(IYield yield, AuthorizeRequestParameters parameters)
        {
            return ServiceContainer.GameService.Authorize(yield, parameters);
        }

        protected override void OnPreAuthorization()
        {
            // Left blank intentionally
        }

        public void OnNonAuthorized()
        {
            var noticeWindow = UserInterfaceContainer.Instance.Get<NoticeWindow>().AssertNotNull();
            noticeWindow.Message.text = $"Authorization with {serverName} server failed.";
            noticeWindow.OkButton.interactable = true;
            noticeWindow.OkButtonClickedAction = LoadedObjects.DestroyAll;
        }

        protected override void OnAuthorized()
        {
            var noticeWindow = UserInterfaceContainer.Instance.Get<NoticeWindow>().AssertNotNull();
            noticeWindow.Hide();

            authorizationStatus = AuthorizationStatus.Succeed;

            onAuthorized?.Invoke();
        }

        protected override IServiceBase GetServiceBase()
        {
            return ServiceContainer.GameService;
        }
    }
}