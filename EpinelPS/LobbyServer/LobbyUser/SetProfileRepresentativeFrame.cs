using EpinelPS.Database;

namespace EpinelPS.LobbyServer.LobbyUser;

[GameRequest("/user/setprofilerepresentativeframe")]
public class SetProfileRepresentativeFrame : LobbyMessage
{
    protected override async Task HandleAsync()
    {
        ReqSetProfileRepresentativeFrame req = await ReadData<ReqSetProfileRepresentativeFrame>();
        User user = GetUser();

        user.ProfileFrameHistoryType = req.ProfileFrameHistoryType;
        user.RepresentativeProfileFrames = [.. req.UserProfileRepresentativeFrames.Select(x => new ProfileRepresentativeFrameData { Slot = x.Slot, FrameTableId = x.FrameTableId })];
        user.RecentAcquireFilterTypes = [.. req.RecentAcquireFilterTypes];

        JsonDb.Save();
        ResSetProfileRepresentativeFrame response = new();

        await WriteDataAsync(response);
    }
}
