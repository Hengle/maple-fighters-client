﻿using System.Threading.Tasks;
using CommonCommunicationInterfaces;
using CommonTools.Coroutines;
using Game.Common;
using Scripts.Containers;

namespace Scripts.Services
{
    public sealed class CharacterService : MicroserviceBase, ICharacterServiceAPI
    {
        public CharacterService() 
            : base(ServiceContainer.GameService)
        {
            // Left blank intentionally
        }

        public async Task<GetCharactersResponseParameters> GetCharacters(IYield yield)
        {
            var parameters = new EmptyParameters();
            return await ServerPeerHandler.SendOperation<EmptyParameters, GetCharactersResponseParameters>
                    (yield, (byte)CharacterOperations.GetCharacters, parameters, MessageSendOptions.DefaultReliable());
        }

        public async Task<CreateCharacterResponseParameters> CreateCharacter(IYield yield, CreateCharacterRequestParameters parameters)
        {
            return await ServerPeerHandler.SendOperation<CreateCharacterRequestParameters, CreateCharacterResponseParameters>
                (yield, (byte)CharacterOperations.CreateCharacter, parameters, MessageSendOptions.DefaultReliable());
        }

        public async Task<RemoveCharacterResponseParameters> RemoveCharacter(IYield yield, RemoveCharacterRequestParameters parameters)
        {
            return await ServerPeerHandler.SendOperation<RemoveCharacterRequestParameters, RemoveCharacterResponseParameters>
                (yield, (byte)CharacterOperations.RemoveCharacter, parameters, MessageSendOptions.DefaultReliable());
        }

        public async Task<CharacterValidationStatus> ValidateCharacter(IYield yield, ValidateCharacterRequestParameters parameters)
        {
            var responseParameters = await ServerPeerHandler.SendOperation<ValidateCharacterRequestParameters, ValidateCharacterResponseParameters>
                (yield, (byte)CharacterOperations.ValidateCharacter, parameters, MessageSendOptions.DefaultReliable());
            if (responseParameters.Status == CharacterValidationStatus.Ok)
            {
                ServiceContainer.GameService.SetServerPeerHandler<GameOperations, GameEvents>();
            }
            return responseParameters.Status;
        }
    }
}