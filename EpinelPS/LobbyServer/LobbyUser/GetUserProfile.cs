using EpinelPS.Data;

namespace EpinelPS.LobbyServer.LobbyUser;

[GameRequest("/User/GetProfile")]
public class GetUserProfile : LobbyMessage
{
    protected override async Task HandleAsync()
    {
        ReqGetProfileData req = await ReadData<ReqGetProfileData>();
        User callingUser = GetUser();
        User? user = GetUser((ulong)req.TargetUsn);
        ResGetProfileData response = new();

        if (user != null)
        {
            response.Data = new NetProfileData
            {
                User = LobbyHandler.CreateWholeUserDataFromDbUser(user),
                LastActionAt = DateTimeOffset.UtcNow.Ticks,
            };
            // Hidden characters (April Fools, NPC units) are not counted as owned Nikkes
            foreach (var corp in user.Characters
                .Select(c => GameData.Instance.CharacterTable.GetValueOrDefault(c.Tid))
                .Where(r => r != null && r.IsVisible)
                .GroupBy(r => (int)r!.Corporation))
            {
                response.Data.CharacterCount.Add(new NetCharacterCount() { CorporationType = corp.Key, Count = corp.Count() });
            }
            response.Data.Language = "en";
            if (user.TowerProgress.TryGetValue(CorporationTowerType.ALL, out int towerFloor))
                response.Data.LastTribeTowerFloor = towerFloor;
            var overclock = user.ResetableData.SimRoomData.CurrentSeasonData.CurrentSeasonHighScore;
            if (overclock.OptionLevel > 0)
            {
                response.Data.SimRoomOverclockHighScoreLevel = overclock.OptionLevel;
                response.Data.SimRoomOverclockHighScoreData.Add(new NetProfileSimRoomOverclockHighScoreData() { Season = overclock.Season, OptionLevel = overclock.OptionLevel, OptionList = { overclock.OptionList } });
            }
            response.Data.ProfileFrameHistoryType = user.ProfileFrameHistoryType;
            response.Data.RecentAcquireFilterTypes.AddRange(user.RecentAcquireFilterTypes);
            foreach (ProfileRepresentativeFrameData frame in user.RepresentativeProfileFrames)
            {
                response.Data.RepresentativeProfileFrames.Add(new NetProfileRepresentativeFrame() { Slot = frame.Slot, FrameTableId = frame.FrameTableId });
            }
            // every frame is treated as owned (see GetProfileFrame)
            foreach (UserFrameRecord frameRecord in GameData.Instance.userFrameTable.Values)
            {
                response.Data.ProfileFrames.Add(new NetProfileFrameData() { FrameTid = frameRecord.Id, AcquiredAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.UtcNow) });
            }
            response.Data.CostumeCount = user.CostumeList.Count;
            response.Data.InfraCoreLv = user.InfraCoreLvl;
            response.Data.LastCampaignNormalStageId = user.LastNormalStageCleared;
            response.Data.LastCampaignHardStageId = user.LastHardStageCleared;
            response.Data.OutpostOpenState = user.MainQuestData.ContainsKey(25);
            response.Data.Desc = GameContext.Users.Where(u => u.ID == UserId).First().Description;
            response.Data.SynchroLv = user.Characters.Max(c => c.Level) < 200 ? user.Characters.Max(c => c.Level) : user.SynchroDeviceLevel;
            response.Data.SynchroSlotCount = user.SynchroSlots.Count(x => x.CharacterSerialNumber != 0);
            for (int i = 0; i < user.RepresentationTeamDataNew.Length; i++)
            {
                long csn = user.RepresentationTeamDataNew[i];
                CharacterModel? c = user.GetCharacterBySerialNumber(csn);

                if (c != null)
                {
                    response.Data.ProfileTeam.Add(new NetProfileTeamSlot() { Slot = i + 1, Default = new() { CostumeId = c.CostumeId, Csn = c.Csn, Grade = c.Grade, Lv = c.Level, Skill1Lv = c.Skill1Lvl, Skill2Lv = c.Skill2Lvl, Tid = c.Tid, UltiSkillLv = c.UltimateLevel } });
                }
            }
            foreach (var research in user.ResearchProgress)
            {
                response.Data.Recycle.Add(new NetUserRecycleRoomData
                {
                    Tid = research.Key,
                    Lv = research.Value.Level
                });
            }

            if (user.CompletedTacticAcademyLessons.Count > 0)
                response.Data.LastTacticAcademyClass = GameData.Instance.GetTacticAcademyLesson(user.CompletedTacticAcademyLessons.Last()).GroupId;
            if (user.CompletedTacticAcademyLessons.Count > 0)
                response.Data.LastTacticAcademyLesson = user.CompletedTacticAcademyLessons.Last();

            //if(user.Memorial.Count > 0)
            //    response.Data.MemorialCount.Add(new NetMemorialCount { Count = user.Memorial.Count });
            response.Data.JukeboxCount = user.JukeboxBgm?.Count ?? 0;
        }

        await WriteDataAsync(response);
    }
}
