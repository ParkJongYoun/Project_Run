using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ProjectRun.Race.Editor
{
    public static class RaceSkillEffectsBuilder
    {
        public const string ConfigPath = "Assets/Resources/RaceSkillEffects/Data_RaceSkillEffects.asset";
        private const string source = "Assets/ArtBundles/Race/SkillEffects/SourcePrefabs/";
        private const string prefabs = "Assets/Resources/RaceSkillEffects/Prefabs/";
        private static readonly Dictionary<string, string> sources = new Dictionary<string, string>
        {
            {"Water","VFX_Water_Area_01_021d5481"}, {"Air","VFX_Air_Area_01_17d553f8"},
            {"Dark","VFX_Dark_Area_01_1f740c28"}, {"Dash","VFX_Dash_b9a7955b"},
            {"Buff","VFX_Buff_Loop_c25cba9f"}, {"Ground","Area_generic_blue_a9bd9767"},
            {"Outbreak","Area_generic_orange_outbreak_9cdb8e04"}, {"Flash","Flash_circle_6a3d2340"},
            {"Speed","SpeedBoost_aside_d2d85243"}, {"Wind","Wind_heavy_14d0d504"}
        };
        private static readonly string[] ids = {"Poring","Minorous","Pecopeco","Lunatic","BaphometJr","Alpaca"};
        private static readonly string[] names = {"포링","미노타우르스","페코페코","루나틱","바포메트 주니어","알파카"};
        private static readonly Color[] colors =
        {
            new Color(.22f,.85f,1), new Color(1,.65f,.12f), new Color(.35f,1,.76f),
            new Color(1,.38f,.78f), new Color(.74f,.23f,1), new Color(1,.9f,.6f)
        };

        [MenuItem("Tools/Project Run/Race skill effects/Enable added effects")]
        public static void Enable() => SetEnabled(true);
        [MenuItem("Tools/Project Run/Race skill effects/Disable added effects")]
        public static void Disable() => SetEnabled(false);
        [MenuItem("Tools/Project Run/Race skill effects/Select effect settings")]
        public static void SelectSettings() => Selection.activeObject = AssetDatabase.LoadAssetAtPath<Data_RaceSkillEffects>(ConfigPath);
        private static void SetEnabled(bool value)
        {
            var config = AssetDatabase.LoadAssetAtPath<Data_RaceSkillEffects>(ConfigPath);
            if (config == null) throw new InvalidOperationException("Build race effects first.");
            Undo.RecordObject(config, "Toggle added race skill effects");
            config.Enabled = value;
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssetIfDirty(config);
        }

        [MenuItem("Tools/Project Run/Race skill effects/Rebuild six effect prefabs")]
        public static void Build()
        {
            Directory.CreateDirectory(prefabs);
            AssetDatabase.Refresh();
            var config = AssetDatabase.LoadAssetAtPath<Data_RaceSkillEffects>(ConfigPath);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<Data_RaceSkillEffects>();
                AssetDatabase.CreateAsset(config, ConfigPath);
            }
            var entries = new List<Data_RaceSkillEffectEntry>();
            for (int i = 0; i < ids.Length; i++)
            {
                var root = new GameObject("P_RaceSkill_" + ids[i]);
                try
                {
                    var color = colors[i];
                    // A readable low ring remains throughout the sprint, including small/far-away runners.
                    Add(root, "Ground", new[]{"area","circle"}, true, i == 3 ? .72f : 1f, color, Vector3.zero, Vector3.zero, "ground");
                    Add(root, "Flash", null, false, i == 1 ? 1.3f : .9f, color, Vector3.zero, Vector3.zero, "burst");
                    if (i == 0 || i == 2 || i == 4)
                    {
                        string key = i == 0 ? "Water" : i == 2 ? "Air" : "Dark";
                        Add(root, key, new[]{"Area Indicator LOOP","Zone Indicator LOOP"}, true, .43f, Color.white, Vector3.up*.04f, Vector3.zero, "ground");
                        if (i == 0)
                            Add(root, key, new[]{"Splashes","Ring Burst LOOP"}, true, .35f, Color.white, Vector3.zero, Vector3.zero, "water");
                        if (i == 2)
                            Add(root, key, new[]{"Impact Hemisphere LOOP"}, true, .42f, Color.white, Vector3.up*.15f, Vector3.zero, "wind");
                        if (i == 4)
                            Add(root, key, new[]{"Thin Smoke LOOP"}, true, .25f, new Color(.65f,.25f,.85f,.38f), Vector3.up*.2f, Vector3.zero, "smoke");
                    }
                    if (i == 1)
                    {
                        Add(root, "Outbreak", null, false, 1.2f, color, Vector3.zero, Vector3.zero, "burst");
                        Add(root, "Wind", new[]{"dust","dust_small"}, true, .45f, new Color(.85f,.66f,.35f,.7f), new Vector3(0,.2f,-1), new Vector3(0,180,0), "dust");
                    }
                    if (i == 2 || i == 3)
                        Add(root, "Dash", new[]{"Ring","Speed","Lines"}, false, i == 3 ? .75f : 1.1f, color, Vector3.up*.65f, new Vector3(0,180,0), "dash");
                    if (i == 5)
                    {
                        Add(root, "Buff", new[]{"Spiral","Stars","Round"}, true, .85f, color, Vector3.up*.5f, Vector3.zero, "stars");
                        Add(root, "Wind", new[]{"dust_small"}, true, .3f, new Color(1,1,1,.4f), new Vector3(0,.3f,-1), new Vector3(0,180,0), "dust");
                    }
                    Add(root, "Speed", null, true, 1, color, new Vector3(0,.65f,-.4f), new Vector3(0,180,0), "sprint");
                    root.SetActive(false);
                    var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabs + root.name + ".prefab");
                    var previous = config.Find(i);
                    entries.Add(new Data_RaceSkillEffectEntry
                    {
                        RunnerId=i, MonsterName=names[i], Enabled=previous == null || previous.Enabled,
                        Prefab=prefab, Scale=previous == null ? 1 : previous.Scale,
                        GroundHeight=previous == null ? .14f : previous.GroundHeight
                    });
                }
                finally { Object.DestroyImmediate(root); }
            }
            config.Runners = entries.ToArray();
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("Six optional race skill effect prefabs built. Toggle: Tools > Project Run > Race skill effects.");
        }

        private static void Add(GameObject parent, string key, string[] selected, bool loop, float scale,
            Color tint, Vector3 position, Vector3 rotation, string role)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(source + sources[key] + ".prefab");
            if (asset == null) throw new InvalidOperationException("Missing effect source: " + key);
            var group = new GameObject("obj_" + role + "_" + key);
            group.transform.SetParent(parent.transform, false);
            group.transform.localPosition = position;
            group.transform.localRotation = Quaternion.Euler(rotation);
            group.transform.localScale = Vector3.one * scale;
            int index = 0;
            foreach (var original in asset.GetComponentsInChildren<ParticleSystem>(true))
            {
                if (selected != null && !selected.Contains(original.name)) continue;
                var clone = Object.Instantiate(original.gameObject, group.transform, false);
                clone.name = "fx_" + role + "_" + index++;
                // Selected particle nodes are independent; no demo scripts, nested lights or duplicate particles.
                foreach (Transform child in clone.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
                foreach (var behaviour in clone.GetComponents<MonoBehaviour>()) Object.DestroyImmediate(behaviour);
                foreach (var light in clone.GetComponents<Light>()) Object.DestroyImmediate(light);
                var ps = clone.GetComponent<ParticleSystem>();
                ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                main.stopAction = ParticleSystemStopAction.None;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                main.simulationSpace = role == "dust" ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
                main.loop = loop;
                main.startDelay = 0;
                main.maxParticles = Math.Min(main.maxParticles, 64);
                if (tint != Color.white)
                {
                    main.startColor = Recolor(main.startColor, tint);
                    var lifetimeColor = ps.colorOverLifetime;
                    if (lifetimeColor.enabled) lifetimeColor.color = Recolor(lifetimeColor.color, Color.white);
                }
                if (loop)
                {
                    main.duration = 2;
                    var emission = ps.emission;
                    emission.enabled = true;
                    if (role == "ground")
                    {
                        main.startLifetime = 2;
                        emission.rateOverTime = 1;
                        emission.SetBursts(new[]{new ParticleSystem.Burst(0,1)});
                    }
                    else if (role == "sprint")
                    {
                        main.startLifetime = .28f;
                        main.startSpeed = 20;
                        main.startSize = .12f;
                        emission.rateOverTime = 30;
                        emission.rateOverDistance = 0;
                        var shape = ps.shape;
                        shape.shapeType = ParticleSystemShapeType.Cone;
                        shape.angle = 4;
                        shape.radius = .95f;
                        shape.position = Vector3.zero;
                        shape.rotation = Vector3.zero;
                        var velocity = ps.velocityOverLifetime; velocity.enabled = false;
                        var force = ps.forceOverLifetime; force.enabled = false;
                    }
                    else if (role == "stars")
                    {
                        main.startSpeedMultiplier *= .2f;
                        main.startLifetime = .6f;
                        emission.rateOverTime = original.name == "Stars" ? 7 : 9;
                    }
                    else if (role == "dust" || role == "smoke")
                    {
                        main.startLifetime = .45f;
                        emission.rateOverTime = role == "smoke" ? 4 : 8;
                        emission.rateOverDistance = 0;
                        var shape = ps.shape; shape.radius = 1;
                    }
                    else if (emission.rateOverTime.constantMax < .1f)
                        emission.rateOverTime = 3;
                }
                var collision = ps.collision; collision.enabled = false;
                var trigger = ps.trigger; trigger.enabled = false;
                var lights = ps.lights; lights.enabled = false;
                var renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (role == "sprint")
                {
                    // The purchased mesh emitter intentionally has an empty main slot and a trail material.
                    // Stretch billboards need that material in the main slot as well.
                    if (renderer.sharedMaterial == null) renderer.sharedMaterial = renderer.trailMaterial;
                    var trails = ps.trails; trails.enabled = false;
                    renderer.renderMode = ParticleSystemRenderMode.Stretch;
                    renderer.lengthScale = 5;
                    renderer.velocityScale = .08f;
                }
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.sortingOrder = 1;
                clone.SetActive(true);
            }
            if (index == 0) throw new InvalidOperationException("No source particles selected: " + key + "/" + role);
        }

        private static Color Recolor(Color value, Color tint)
        {
            float brightness = Mathf.Max(value.r, Mathf.Max(value.g, value.b));
            return new Color(tint.r * brightness, tint.g * brightness, tint.b * brightness, value.a * tint.a);
        }
        private static Gradient Recolor(Gradient value, Color tint)
        {
            var result = new Gradient();
            result.SetKeys(value.colorKeys.Select(x => new GradientColorKey(Recolor(x.color,tint),x.time)).ToArray(),
                value.alphaKeys.Select(x => new GradientAlphaKey(x.alpha*tint.a,x.time)).ToArray());
            result.mode = value.mode;
            return result;
        }
        private static ParticleSystem.MinMaxGradient Recolor(ParticleSystem.MinMaxGradient value, Color tint)
        {
            var mode = value.mode;
            if (mode == ParticleSystemGradientMode.Color) return new ParticleSystem.MinMaxGradient(Recolor(value.color,tint));
            if (mode == ParticleSystemGradientMode.TwoColors) return new ParticleSystem.MinMaxGradient(Recolor(value.colorMin,tint),Recolor(value.colorMax,tint));
            if (mode == ParticleSystemGradientMode.TwoGradients) return new ParticleSystem.MinMaxGradient(Recolor(value.gradientMin,tint),Recolor(value.gradientMax,tint));
            var result = new ParticleSystem.MinMaxGradient(Recolor(value.gradient,tint)); result.mode = mode; return result;
        }
    }
}
