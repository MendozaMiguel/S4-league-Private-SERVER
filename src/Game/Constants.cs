﻿ // ReSharper disable once CheckNamespace
namespace Netsphere
{
    internal enum PlayerSetting
    {
        AllowCombiInvite,
        AllowFriendRequest,
        AllowRoomInvite,
        AllowInfoRequest
    }

    internal enum GameRuleState
    {
        Waiting,
        Playing,
        EnteringResult,
        Result,

        FirstHalf,
        EnteringHalfTime,
        HalfTime,
        SecondHalf,

        Prepare

    }

    enum GameRuleStateTrigger
    {
        StartPrepare,
        StartGame,
        EndGame,
        StartResult,
        StartHalfTime,
        StartSecondHalf
    }
}
