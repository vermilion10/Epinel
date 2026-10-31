using EpinelPS.Data;
using EpinelPS.Database;
using EpinelPS.Utils;

namespace EpinelPS.LobbyServer.Event;

[GameRequest("/event/boxgacha/execute")]
public class ExecuteEventBoxGacha : LobbyMessage
{
    private static readonly Random random = new();

    protected override async Task HandleAsync()
    {
        // from client: {"EventId":10051,"CurrentCount":1}
        ReqExecuteEventBoxGacha req = await ReadData<ReqExecuteEventBoxGacha>();
        User user = GetUser();

        ResExecuteEventBoxGacha response = new();

        EventBoxGachaRecord? gacha = GameData.Instance.EventBoxGachaTable.Values.FirstOrDefault(x => x.EventId == req.EventId);
        if (gacha == null)
        {
            Logging.WriteLine($"Box gacha not found for event {req.EventId}", LogType.Warning);
            await WriteDataAsync(response);
            return;
        }

        if (!user.EventBoxGachaOrders.TryGetValue(req.EventId, out List<int>? drawn))
        {
            drawn = [];
            user.EventBoxGachaOrders[req.EventId] = drawn;
        }

        // the n-th pull of the box has its own price and its own rates
        int count = drawn.Count + 1;
        List<EventBoxGachaRewardRecord> rewards = [.. GameData.Instance.EventBoxGachaRewardTable.Values.Where(x => x.Group == gacha.GachaRewardGroup)];
        if (drawn.Count >= rewards.Count)
        {
            response.RewardOrders.AddRange(drawn);
            await WriteDataAsync(response);
            return;
        }

        EventBoxGachaPriceRecord? price = GameData.Instance.EventBoxGachaPriceTable.Values.FirstOrDefault(x => x.Group == gacha.PriceGroup && x.Count == count);
        if (price != null && price.ItemType == RewardType.Item && price.ItemCount > 0)
        {
            DbItemData? ticket = user.Items.FirstOrDefault(x => x.ItemType == price.ItemId && x.Count >= price.ItemCount);
            if (ticket == null)
            {
                Logging.WriteLine($"Not enough box gacha tickets ({price.ItemId} x{price.ItemCount})", LogType.Warning);
                response.RewardOrders.AddRange(drawn);
                await WriteDataAsync(response);
                return;
            }
            ticket.Count -= price.ItemCount;
            response.Ticket = NetUtils.UserItemDataToNet(ticket);
        }

        List<EventBoxGachaRewardRecord> remaining = [.. rewards.Where(x => !drawn.Contains(x.Order))];
        Dictionary<int, int> rates = GameData.Instance.EventBoxGachaProbTable.Values
            .Where(x => x.Group == gacha.ProbGroup && x.Count == count)
            .ToDictionary(x => x.Order, x => x.Rate);
        int total = remaining.Sum(x => rates.GetValueOrDefault(x.Order));

        EventBoxGachaRewardRecord picked = remaining[random.Next(remaining.Count)];
        if (total > 0)
        {
            int roll = random.Next(total);
            foreach (EventBoxGachaRewardRecord reward in remaining)
            {
                roll -= rates.GetValueOrDefault(reward.Order);
                if (roll < 0)
                {
                    picked = reward;
                    break;
                }
            }
        }

        drawn.Add(picked.Order);

        NetRewardData netReward = new();
        RewardUtils.AddSingleObject(user, ref netReward, picked.ItemId, picked.ItemType, picked.ItemCount);
        response.Reward = netReward;
        response.RewardOrders.AddRange(drawn);

        JsonDb.Save();
        await WriteDataAsync(response);
    }
}
