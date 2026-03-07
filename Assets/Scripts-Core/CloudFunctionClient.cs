using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Firebase.Functions;
using UnityEngine;

public class CloudFunctionClient : MonoBehaviour
{
    private FirebaseFunctions _functions;

    private void Awake()
    {
        _functions = FirebaseFunctions.DefaultInstance;
    }

    public async Task<Dictionary<string, object>> StartRunAsync()
    {
        var callable = _functions.GetHttpsCallable("startRun");
        var result = await callable.CallAsync(new Dictionary<string, object>());
        return ToDictionary(result.Data);
    }

    public async Task<Dictionary<string, object>> EndRunAsync(string runId, string result, int floorsCleared, bool bossDefeated)
    {
        var payload = new Dictionary<string, object>
        {
            { "runId", runId },
            { "result", result },
            { "floorsCleared", floorsCleared },
            { "bossDefeated", bossDefeated }
        };

        var callable = _functions.GetHttpsCallable("endRun");
        var response = await callable.CallAsync(payload);
        return ToDictionary(response.Data);
    }

    private static Dictionary<string, object> ToDictionary(object data)
    {
        if (data is Dictionary<string, object> dict)
        {
            return dict;
        }

        if (data is IDictionary<object, object> objDict)
        {
            var mapped = new Dictionary<string, object>();
            foreach (var kv in objDict)
            {
                mapped[kv.Key.ToString()] = kv.Value;
            }
            return mapped;
        }

        return new Dictionary<string, object>();
    }

    public static int ReadInt(Dictionary<string, object> data, string key, int fallback)
    {
        if (!data.TryGetValue(key, out var raw) || raw == null)
        {
            return fallback;
        }

        if (raw is long longVal) return (int)longVal;
        if (raw is int intVal) return intVal;
        if (raw is double doubleVal) return Mathf.RoundToInt((float)doubleVal);

        if (int.TryParse(raw.ToString(), out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    public static bool ReadBool(Dictionary<string, object> data, string key, bool fallback)
    {
        if (!data.TryGetValue(key, out var raw) || raw == null)
        {
            return fallback;
        }

        if (raw is bool boolVal) return boolVal;
        if (bool.TryParse(raw.ToString(), out var parsed)) return parsed;

        return fallback;
    }
}
