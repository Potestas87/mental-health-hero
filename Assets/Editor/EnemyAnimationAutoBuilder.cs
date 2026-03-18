#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class EnemyAnimationAutoBuilder
{
    private const string SpritesRoot = "Assets/Sprites";
    private const string AnimRoot = "Assets/Animations/Enemies";
    private const string ControllerRoot = "Assets/Animations/Controllers/Enemies";

    private static readonly Dictionary<string, string> ArchetypeToSpriteFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Chaser", "Chaser-Enemy" },
        { "Tank", "Tank-Enemy" },
        { "Bruiser", "Bruiser-Enemy" },
        { "Skirmisher", "Enemy-Skirmisher" },
        { "RangedProxy", "RangedProxy-Enemy" },
        { "Boss", "Boss-Archetypes" }
    };

    private static readonly Dictionary<string, string> ArchetypeToPrefabPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "Chaser", "Assets/Prefabs/Enemy-Chaser.prefab" },
        { "Tank", "Assets/Prefabs/Enemy-Tank.prefab" },
        { "Bruiser", "Assets/Prefabs/Enemy-Bruiser.prefab" },
        { "Skirmisher", "Assets/Prefabs/Enemy-Skirmisher.prefab" },
        { "RangedProxy", "Assets/Prefabs/Enemy-RangedProxy.prefab" },
        { "Boss", "Assets/Prefabs/BossEnemy.prefab" }
    };

    private static readonly Dictionary<string, Vector2> DirectionVectors = new Dictionary<string, Vector2>(StringComparer.OrdinalIgnoreCase)
    {
        { "N", new Vector2(0f, 1f) },
        { "NE", new Vector2(0.7071f, 0.7071f) },
        { "E", new Vector2(1f, 0f) },
        { "SE", new Vector2(0.7071f, -0.7071f) },
        { "S", new Vector2(0f, -1f) },
        { "SW", new Vector2(-0.7071f, -0.7071f) },
        { "W", new Vector2(-1f, 0f) },
        { "NW", new Vector2(-0.7071f, 0.7071f) }
    };

    [MenuItem("Tools/Enemies/Build Enemy Animation Controllers")]
    public static void BuildAll()
    {
        EnsureFolders();

        foreach (var kv in ArchetypeToSpriteFolder)
        {
            var archetype = kv.Key;
            var spriteFolder = Path.Combine(SpritesRoot, kv.Value).Replace("\\", "/");
            BuildForArchetype(archetype, spriteFolder);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("EnemyAnimationAutoBuilder: Build complete.");
    }

    private static void BuildForArchetype(string archetype, string spriteFolder)
    {
        var idleFolder = FindMotionFolder(spriteFolder, "Idle");
        var moveFolder = FindMotionFolder(spriteFolder, "Move");

        if (string.IsNullOrEmpty(idleFolder) || string.IsNullOrEmpty(moveFolder))
        {
            Debug.LogWarning($"EnemyAnimationAutoBuilder: Missing idle/move folders for {archetype} under {spriteFolder}");
            return;
        }

        var clipOutputFolder = $"{AnimRoot}/{archetype}";
        EnsureFolder(clipOutputFolder);

        var idleClips = BuildDirectionalClips(archetype, "Idle", idleFolder, clipOutputFolder);
        var moveClips = BuildDirectionalClips(archetype, "Move", moveFolder, clipOutputFolder);

        var controllerPath = $"{ControllerRoot}/{archetype}.controller";
        var controller = BuildController(archetype, controllerPath, idleClips, moveClips);
        AssignControllerToPrefab(archetype, controller);
    }

    private static Dictionary<string, AnimationClip> BuildDirectionalClips(string archetype, string action, string motionRoot, string outputFolder)
    {
        var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        var directionFolders = Directory.GetDirectories(motionRoot)
            .Select(p => p.Replace("\\", "/"))
            .OrderBy(p => p)
            .ToList();

        foreach (var dirFolder in directionFolders)
        {
            var directionToken = ToDirectionToken(Path.GetFileName(dirFolder));
            if (string.IsNullOrEmpty(directionToken))
            {
                continue;
            }

            if (!DirectionVectors.ContainsKey(directionToken))
            {
                continue;
            }

            var sprites = LoadSpritesInFolder(dirFolder);
            if (sprites.Count == 0)
            {
                continue;
            }

            var clipName = $"{archetype}-{action}{directionToken}";
            var clipPath = $"{outputFolder}/{clipName}.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            ApplySpriteFramesToClip(clip, sprites, action);
            clips[directionToken] = clip;
        }

        return clips;
    }

    private static void ApplySpriteFramesToClip(AnimationClip clip, List<Sprite> sprites, string action)
    {
        var binding = new EditorCurveBinding
        {
            type = typeof(SpriteRenderer),
            path = string.Empty,
            propertyName = "m_Sprite"
        };

        var keys = new ObjectReferenceKeyframe[sprites.Count];
        var fps = string.Equals(action, "Move", StringComparison.OrdinalIgnoreCase) ? 12f : 10f;
        for (var i = 0; i < sprites.Count; i++)
        {
            keys[i] = new ObjectReferenceKeyframe
            {
                time = i / fps,
                value = sprites[i]
            };
        }

        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static AnimatorController BuildController(
        string archetype,
        string controllerPath,
        Dictionary<string, AnimationClip> idleClips,
        Dictionary<string, AnimationClip> moveClips)
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath) != null)
        {
            AssetDatabase.DeleteAsset(controllerPath);
        }

        var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        controller.parameters = new[]
        {
            new AnimatorControllerParameter { name = "Speed", type = AnimatorControllerParameterType.Float, defaultFloat = 0f },
            new AnimatorControllerParameter { name = "FaceX", type = AnimatorControllerParameterType.Float, defaultFloat = 0f },
            new AnimatorControllerParameter { name = "FaceY", type = AnimatorControllerParameterType.Float, defaultFloat = -1f }
        };

        var sm = controller.layers[0].stateMachine;

        var idleState = sm.AddState($"{archetype}-Idle");
        var moveState = sm.AddState($"{archetype}-Move");
        sm.defaultState = idleState;

        var idleTree = new BlendTree
        {
            name = $"{archetype}-IdleTree",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "FaceX",
            blendParameterY = "FaceY"
        };
        AssetDatabase.AddObjectToAsset(idleTree, controller);
        idleState.motion = idleTree;

        var moveTree = new BlendTree
        {
            name = $"{archetype}-MoveTree",
            blendType = BlendTreeType.SimpleDirectional2D,
            blendParameter = "FaceX",
            blendParameterY = "FaceY"
        };
        AssetDatabase.AddObjectToAsset(moveTree, controller);
        moveState.motion = moveTree;

        AddDirectionalMotions(idleTree, idleClips);
        AddDirectionalMotions(moveTree, moveClips);

        var idleToMove = idleState.AddTransition(moveState);
        idleToMove.hasExitTime = false;
        idleToMove.duration = 0f;
        idleToMove.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

        var moveToIdle = moveState.AddTransition(idleState);
        moveToIdle.hasExitTime = false;
        moveToIdle.duration = 0f;
        moveToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void AddDirectionalMotions(BlendTree tree, Dictionary<string, AnimationClip> clips)
    {
        foreach (var dir in DirectionVectors)
        {
            if (!clips.TryGetValue(dir.Key, out var clip) || clip == null)
            {
                continue;
            }

            tree.AddChild(clip, dir.Value);
        }
    }

    private static void AssignControllerToPrefab(string archetype, RuntimeAnimatorController controller)
    {
        if (!ArchetypeToPrefabPath.TryGetValue(archetype, out var prefabPath))
        {
            return;
        }

        var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var animator = prefabRoot.GetComponent<Animator>();
            if (animator == null)
            {
                animator = prefabRoot.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(prefabRoot);
        }
    }

    private static List<Sprite> LoadSpritesInFolder(string folder)
    {
        var guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        var sprites = new List<Sprite>();
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null)
            {
                sprites.Add(sprite);
            }
        }

        sprites.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        return sprites;
    }

    private static string FindMotionFolder(string archetypeFolder, string motionName)
    {
        if (!Directory.Exists(archetypeFolder))
        {
            return null;
        }

        var directories = Directory.GetDirectories(archetypeFolder)
            .Select(p => p.Replace("\\", "/"))
            .ToList();

        return directories.FirstOrDefault(d =>
            Path.GetFileName(d).IndexOf($"-{motionName}", StringComparison.OrdinalIgnoreCase) >= 0 ||
            Path.GetFileName(d).IndexOf(motionName, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static string ToDirectionToken(string folderName)
    {
        var normalized = folderName.Trim().ToLowerInvariant().Replace("_", "-").Replace(" ", "-");
        switch (normalized)
        {
            case "n":
            case "north":
                return "N";
            case "ne":
            case "north-east":
            case "northeast":
                return "NE";
            case "e":
            case "east":
                return "E";
            case "se":
            case "south-east":
            case "southeast":
                return "SE";
            case "s":
            case "south":
                return "S";
            case "sw":
            case "south-west":
            case "southwest":
                return "SW";
            case "w":
            case "west":
                return "W";
            case "nw":
            case "north-west":
            case "northwest":
                return "NW";
            default:
                return null;
        }
    }

    private static void EnsureFolders()
    {
        EnsureFolder(AnimRoot);
        EnsureFolder(ControllerRoot);
    }

    private static void EnsureFolder(string path)
    {
        var parts = path.Split('/');
        var current = parts[0];
        for (var i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }
}
#endif
