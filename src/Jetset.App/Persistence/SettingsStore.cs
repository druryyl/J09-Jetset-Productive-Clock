using System.Globalization;
using Jetset.App.Models;
using Microsoft.Data.Sqlite;

namespace Jetset.App.Persistence;

public sealed class SettingsStore
{
    private readonly SqliteConnectionFactory _factory;

    public SettingsStore(SqliteConnectionFactory factory)
    {
        _factory = factory;
    }

    public AppSettings Load()
    {
        var settings = new AppSettings();
        using var connection = _factory.Create();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM AppSetting;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var key = reader.GetString(0);
            var value = reader.GetString(1);
            Apply(settings, key, value);
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        using var connection = _factory.Create();
        using var tx = connection.BeginTransaction();

        Upsert(connection, tx, "AlwaysOnTop", settings.AlwaysOnTop.ToString());
        Upsert(connection, tx, "CompactMode", settings.CompactMode.ToString());
        Upsert(connection, tx, "Use24HourClock", settings.Use24HourClock.ToString());
        Upsert(connection, tx, "ShowSeconds", settings.ShowSeconds.ToString());
        Upsert(connection, tx, "SoundOnCountdownComplete", settings.SoundOnCountdownComplete.ToString());
        Upsert(connection, tx, "StartWithWindows", settings.StartWithWindows.ToString());
        Upsert(connection, tx, "UseDarkTheme", settings.UseDarkTheme.ToString());
        Upsert(connection, tx, "AutoPauseWhenIdle", settings.AutoPauseWhenIdle.ToString());
        Upsert(connection, tx, "IdleTimeoutMinutes", ClampIdleTimeout(settings.IdleTimeoutMinutes).ToString(CultureInfo.InvariantCulture));
        Upsert(connection, tx, "AutoResumeAfterIdle", settings.AutoResumeAfterIdle.ToString());
        Upsert(connection, tx, "WindowLeft", settings.WindowLeft.ToString(CultureInfo.InvariantCulture));
        Upsert(connection, tx, "WindowTop", settings.WindowTop.ToString(CultureInfo.InvariantCulture));
        Upsert(connection, tx, "WindowWidth", settings.WindowWidth.ToString(CultureInfo.InvariantCulture));
        Upsert(connection, tx, "WindowHeight", settings.WindowHeight.ToString(CultureInfo.InvariantCulture));

        tx.Commit();
    }

    private static void Upsert(SqliteConnection connection, SqliteTransaction tx, string key, string value)
    {
        using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText =
            """
            INSERT INTO AppSetting (Key, Value) VALUES (@key, @value)
            ON CONFLICT(Key) DO UPDATE SET Value = excluded.Value;
            """;
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value);
        command.ExecuteNonQuery();
    }

    private static void Apply(AppSettings settings, string key, string value)
    {
        switch (key)
        {
            case "AlwaysOnTop":
                settings.AlwaysOnTop = bool.Parse(value);
                break;
            case "CompactMode":
                settings.CompactMode = bool.Parse(value);
                break;
            case "Use24HourClock":
                settings.Use24HourClock = bool.Parse(value);
                break;
            case "ShowSeconds":
                settings.ShowSeconds = bool.Parse(value);
                break;
            case "SoundOnCountdownComplete":
                settings.SoundOnCountdownComplete = bool.Parse(value);
                break;
            case "StartWithWindows":
                settings.StartWithWindows = bool.Parse(value);
                break;
            case "UseDarkTheme":
                settings.UseDarkTheme = bool.Parse(value);
                break;
            case "AutoPauseWhenIdle":
                settings.AutoPauseWhenIdle = bool.Parse(value);
                break;
            case "IdleTimeoutMinutes":
                settings.IdleTimeoutMinutes = ClampIdleTimeout(int.Parse(value, CultureInfo.InvariantCulture));
                break;
            case "AutoResumeAfterIdle":
                settings.AutoResumeAfterIdle = bool.Parse(value);
                break;
            case "WindowLeft":
                settings.WindowLeft = double.Parse(value, CultureInfo.InvariantCulture);
                break;
            case "WindowTop":
                settings.WindowTop = double.Parse(value, CultureInfo.InvariantCulture);
                break;
            case "WindowWidth":
                settings.WindowWidth = double.Parse(value, CultureInfo.InvariantCulture);
                break;
            case "WindowHeight":
                settings.WindowHeight = double.Parse(value, CultureInfo.InvariantCulture);
                break;
        }
    }

    private static int ClampIdleTimeout(int minutes) => Math.Clamp(minutes, 1, 60);
}
