using System.Globalization;
using System.Reflection;
using System.Text;
using osu.Framework.Bindables;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Rulesets.Mods;

namespace SosuBot.PerformanceCalculator;

internal static class PPCalculationCacheKey
{
    public static string Create(
        int beatmapId,
        int rulesetId,
        int? hitObjectsLimit,
        IEnumerable<Mod> mods)
    {
        StringBuilder key = new();
        key.Append(rulesetId.ToString(CultureInfo.InvariantCulture));
        key.Append(':');
        key.Append(beatmapId.ToString(CultureInfo.InvariantCulture));
        key.Append(':');
        key.Append(hitObjectsLimit?.ToString(CultureInfo.InvariantCulture) ?? "full");
        key.Append(':');

        foreach (Mod mod in mods.OrderBy(mod => mod.Acronym, StringComparer.Ordinal))
        {
            key.Append(mod.Acronym);

            foreach (var setting in mod.GetSettingsSourceProperties()
                         .OrderBy(pair => pair.Item2.Name, StringComparer.Ordinal))
            {
                PropertyInfo property = setting.Item2;
                if (property.GetValue(mod) is not IBindable bindable || bindable.IsDefault)
                    continue;

                key.Append('(');
                key.Append(property.Name.ToSnakeCase());
                key.Append('=');
                key.Append(Convert.ToString(bindable.GetUnderlyingSettingValue(), CultureInfo.InvariantCulture) ?? "null");
                key.Append(')');
            }

            key.Append(';');
        }

        return key.ToString();
    }
}
