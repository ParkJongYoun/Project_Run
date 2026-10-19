using System;
using UnityEditor;
using UnityEngine;
using ProjectRun.Ranch;

namespace ProjectRun.Editor
{
    // Repairs the generated assets in place, preserving meshes, UVs and prefab references.
    public static class RanchSurfaceRepair
    {
        public static readonly string[] FacilityIds = { "feed_store", "shelter", "training", "house", "lamp", "flowerbed", "broadleaf", "pine", "rock", "fence", "gate" };

        public static void ApplyNormal(string id)
        {
            string folder = RanchGeneratedImporter.GeneratedRoot + "/" + id;
            string path = folder + "/Textures/Normal.png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing ranch normal: " + path);
            bool changed = importer.textureType != TextureImporterType.NormalMap || importer.sRGBTexture ||
                !importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear || importer.anisoLevel != 4;
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            // The generated maps use tangent-space +Y. Do not invert the green channel.
            if (changed) importer.SaveAndReimport();
            var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!material.name.StartsWith("M_RanchGenerated_") || !material.HasProperty("_BumpMap")) continue;
                material.SetTexture("_BumpMap", normal);
                material.SetFloat("_BumpScale", .8f);
                material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
            }
            string painted = RanchPaintedLook.ArtRoot + "/Materials/M_RanchPainted_" + id + "_0.mat";
            var surface = AssetDatabase.LoadAssetAtPath<Material>(painted);
            if (surface != null && surface.HasProperty("_BumpMap"))
            {
                surface.SetTexture("_BumpMap", normal); surface.SetFloat("_BumpScale", .85f);
                EditorUtility.SetDirty(surface);
            }
        }

        public static void ConfigureResident(GameObject monster, string id, Sys_RanchPlacement placement, int index)
        {
            var resident = monster.GetComponent<Sys_RanchResident>();
            if (resident == null) resident = monster.AddComponent<Sys_RanchResident>();
            resident.Placement = placement;
            resident.TargetAnimator = monster.GetComponentInChildren<Animator>(true);
            string folder = "Assets/ArtBundles/Characters/CartoonMonsters/" + id + "/Animations/" + id;
            resident.IdleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "_Idle.anim");
            resident.LocomotionClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(folder + "_Run.anim");
            resident.MoveSpeed = id == "Minorous" ? .68f : id == "GiantPoring" ? .8f : .95f;
            resident.AnimationRate = id == "Minorous" ? .78f : .65f;
            resident.Clearance = .64f;
            resident.Seed = 41 + index*97;
            resident.enabled = true;
            EditorUtility.SetDirty(monster);
        }
    }
}
