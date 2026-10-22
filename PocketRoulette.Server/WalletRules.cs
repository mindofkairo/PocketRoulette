using System.Text.Json;

namespace PocketRoulette.Server;

public static class WalletRules
{
    public static List<MoneyDebit>? Plan(IReadOnlyDictionary<string, int> stacks, long price)
    {
        if (price < 1 || stacks.Values.Sum(value => (long)Math.Max(0, value)) < price) return null;
        var result = new List<MoneyDebit>();
        foreach (var stack in stacks.OrderBy(stack => stack.Value))
        {
            if (price == 0) break;
            var taken = (int)Math.Min(price, Math.Max(0, stack.Value));
            if (taken == 0) continue;
            result.Add(new MoneyDebit { ItemId = stack.Key, Before = stack.Value, After = stack.Value - taken });
            price -= taken;
        }
        return result;
    }

    // Recovery accepts an untouched stack or this journal's exact post-payment state.
    // Anything else requires inspection rather than guessing and charging again.
    public static bool NeedsDebit(MoneyDebit debit, int? current)
    {
        if (current == debit.After || current == null && debit.After == 0) return false;
        if (current == debit.Before) return true;
        throw new InvalidOperationException("A pending buy-in's rouble stacks changed unexpectedly; payment was stopped for recovery.");
    }

    public static List<MoneyDebit> PendingDebits(IEnumerable<MoneyDebit> debits, IReadOnlyDictionary<string, int> stacks) =>
        // Materialize the complete validation before the caller mutates any stack.
        debits.Where(debit => NeedsDebit(debit, stacks.TryGetValue(debit.ItemId, out var count) ? count : null)).ToList();

    public static void VerifySavedPayment(string json, IReadOnlyCollection<MoneyDebit> debits)
    {
        using var document = JsonDocument.Parse(json);
        var inventory = Property(Property(Property(document.RootElement, "characters"), "pmc"), "Inventory");
        var items = Property(inventory, "items");
        if (items.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Saved profile inventory is unavailable.");
        var wanted = debits.Select(debit => debit.ItemId).ToHashSet();
        var counts = new Dictionary<string, double>();
        foreach (var item in items.EnumerateArray())
        {
            var id = Property(item, "_id").GetString() ?? "";
            if (!wanted.Contains(id)) continue;
            var upd = Property(item, "upd", false);
            var count = upd.ValueKind == JsonValueKind.Object ? Property(upd, "StackObjectsCount", false) : default;
            counts.Add(id, count.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null ? 1 : count.GetDouble());
        }
        if (debits.Any(debit => counts.TryGetValue(debit.ItemId, out var count) ? count != debit.After : debit.After != 0))
            throw new InvalidOperationException("The stash payment is not confirmed in the saved profile. No new spins were issued. The payment journal is retained; restart the server before retrying if its profile save failed.");
    }

    private static JsonElement Property(JsonElement value, string name, bool required = true)
    {
        if (value.ValueKind == JsonValueKind.Object)
            foreach (var property in value.EnumerateObject())
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return property.Value;
        if (required) throw new InvalidDataException("Saved profile is missing " + name + ". Payment remains pending.");
        return default;
    }
}
