using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JGD.Level;

namespace JGD.Level.EditorTools
{
    public class PuzzleImporterWindow : EditorWindow
    {
        TextAsset jsonFile;
        string jsonText = "";
        PuzzleRegistry registry;
        float tileSize = 50f;
        bool gridLines = true;
        Vector2 scroll;
        string msg = "粘贴网页编辑器导出的 JSON 开始";
        MessageType msgType = MessageType.Info;

        [MenuItem("JGD/拼图关卡导入器")]
        static void Open() => GetWindow<PuzzleImporterWindow>("拼图导入器");

        void OnGUI()
        {
            EditorGUILayout.HelpBox("流程：网页编辑器复制 JSON → ①导入到容器 → 场景建模 → ②存入容器 → ③预览网格 → ④生成布局", MessageType.Info);

            jsonFile = (TextAsset)EditorGUILayout.ObjectField("JSON 文件(TextAsset)", jsonFile, typeof(TextAsset), false);
            if (jsonFile != null) jsonText = jsonFile.text;
            jsonText = EditorGUILayout.TextArea(jsonText, GUILayout.MinHeight(70));
            registry = (PuzzleRegistry)EditorGUILayout.ObjectField("容器 PuzzleRegistry", registry, typeof(PuzzleRegistry), false);

            using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(jsonText)))
                if (GUILayout.Button("① 导入到容器", GUILayout.Height(26))) Import();

            if (registry == null) { EditorGUILayout.HelpBox(msg, msgType); return; }

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("拼图条目", EditorStyles.boldLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(140));
            foreach (var e in registry.entries)
            {
                EditorGUILayout.BeginHorizontal("box");
                EditorGUILayout.LabelField(new GUIContent(e.pieceName, e.pieceName), GUILayout.MinWidth(80));
                EditorGUILayout.LabelField(e.prefab ? "已入库" : "未建模", GUILayout.Width(52));
                e.prefab = (GameObject)EditorGUILayout.ObjectField(e.prefab, typeof(GameObject), false, GUILayout.Width(110));
                e.modeledTileSize = EditorGUILayout.FloatField(e.modeledTileSize, GUILayout.Width(56));
                if (GUILayout.Button("存入选中", GUILayout.Width(72))) StoreSelected(e);
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            tileSize = EditorGUILayout.FloatField(new GUIContent("当前地块大小(米)"), tileSize);
            gridLines = EditorGUILayout.Toggle(new GUIContent("生成参考网格线"), gridLines);

            using (new EditorGUI.DisabledScope(registry.level.pieces.Count == 0))
            {
                if (GUILayout.Button("② 预览网格（Scene 视窗）", GUILayout.Height(24)))
                {
                    var __pv = PuzzleLevelBuilder.TogglePreviewGO();
                    if (__pv != null)
                    {
                        var __d = __pv.AddComponent<JGD.Level.EditorTools.PuzzlePreviewDrawer>();
                        __d.registry = registry; __d.gridLines = gridLines; __d.tileSize = tileSize;
                        if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
                        SetMsg("预览已开启：切到 Scene 视窗查看；再点一次关闭", MessageType.Info);
                    }
                    else SetMsg("预览已关闭", MessageType.Info);
                    SetMsg("已在 Scene 视窗绘制预览（切换到 Scene 页签查看）；再点一次可关闭", MessageType.Info);
                }
                using (new EditorGUI.DisabledScope(!PuzzleLevelBuilder.HasPreview))
                    if (GUILayout.Button("③ 生成布局", GUILayout.Height(26)))
                    {
                        PuzzleLevelBuilder.Build(registry, tileSize, gridLines);
                        MarkSceneDirty();
                        SetMsg("布局已生成在 PuzzleLevel 根节点下", MessageType.Info);
                    }
                if (GUILayout.Button("清空布局", GUILayout.Height(20)))
                {
                    PuzzleLevelBuilder.Clear();
                    MarkSceneDirty();
                    // 预览状态已在上方分支中提示
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(msg, msgType);
        }

        void Import()
        {
            try
            {
                var data = LevelData.FromJson(jsonText);
                if (registry == null)
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
                    const string path = "Assets/Data/PuzzleRegistry.asset";
                    registry = AssetDatabase.LoadAssetAtPath<PuzzleRegistry>(path);
                    if (registry == null)
                    {
                        registry = CreateInstance<PuzzleRegistry>();
                        AssetDatabase.CreateAsset(registry, path);
                    }
                }
                registry.EnsureEntries(data);
                tileSize = data.cfg.tileSize;
                EditorUtility.SetDirty(registry);
                AssetDatabase.SaveAssets();
                SetMsg($"导入成功：{data.pieces.Count} 张拼图（地块大小 {data.cfg.tileSize}m）。逐个建模后点「存入选中」。", MessageType.Info);
            }
            catch (Exception ex)
            {
                SetMsg("导入失败：" + ex.Message, MessageType.Error);
            }
        }

        void StoreSelected(PuzzleRegistry.PrefabEntry e)
        {
            var go = Selection.activeGameObject;
            if (go == null) { SetMsg("请先在 Hierarchy 中选中该拼图的根节点", MessageType.Warning); return; }
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Regions")) AssetDatabase.CreateFolder("Assets/Prefabs", "Regions");
            var path = $"Assets/Prefabs/Regions/{e.pieceName}.prefab";
            // 连接为 Prefab 实例：场景节点保留（可自行移到远处留存），资产入库
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction);
            if (e.modeledTileSize <= 0) e.modeledTileSize = tileSize;
            e.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            e.done = true;
            EditorUtility.SetDirty(registry);
            AssetDatabase.SaveAssets();
            SetMsg($"已入库：{e.pieceName} → {path}（场景节点已连接为 Prefab 实例，可移走留存）", MessageType.Info);
        }

        void MarkSceneDirty() => EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        void SetMsg(string text, MessageType type)
        {
            msg = text; msgType = type;
            Repaint();
        }
    }
}
