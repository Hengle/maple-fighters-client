﻿using System.Threading.Tasks;
using CommonTools.Coroutines;
using Shared.Game.Common;

namespace Scripts.Services
{
    public interface IGameService : IServiceBase
    {
        Task<EnterWorldResponseParameters> EnterWorld(IYield yield);

        void UpdatePosition(UpdatePositionRequestParameters parameters);
        void UpdatePlayerState(UpdatePlayerStateRequestParameters parameters);

        Task<AuthenticationStatus> Authenticate(IYield yield);

        Task<FetchCharactersResponseParameters> FetchCharacters(IYield yield);
        Task<ValidateCharacterStatus> ValidateCharacter(IYield yield, ValidateCharacterRequestParameters parameters);
        Task<CreateCharacterResponseParameters> CreateCharacter(IYield yield, CreateCharacterRequestParameters parameters);
        Task<RemoveCharacterResponseParameters> RemoveCharacter(IYield yield, RemoveCharacterRequestParameters parameters);
        Task<ChangeSceneResponseParameters> ChangeScene(IYield yield, ChangeSceneRequestParameters parameters);

        UnityEvent<SceneObjectAddedEventParameters> SceneObjectAdded { get; }
        UnityEvent<SceneObjectRemovedEventParameters> SceneObjectRemoved { get; }
        UnityEvent<SceneObjectsAddedEventParameters> SceneObjectsAdded { get; }
        UnityEvent<SceneObjectsRemovedEventParameters> SceneObjectsRemoved { get; }

        UnityEvent<SceneObjectPositionChangedEventParameters> PositionChanged { get; }
        UnityEvent<PlayerStateChangedEventParameters> PlayerStateChanged { get; }
    }
}