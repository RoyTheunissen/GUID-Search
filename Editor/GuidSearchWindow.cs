using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using EditorUtility = UnityEditor.EditorUtility;
using Object = UnityEngine.Object;

namespace RoyTheunissen.GuidSearch
{
    public sealed class GuidSearchWindow : EditorWindow
    {
        [Flags]
        public enum AssetTypes
        {
            Scenes = 1 << 0,
            Prefabs = 1 << 1,
            ScriptableObjects = 1 << 2,
            Materials = 1 << 3,
            Models = 1 << 4,
        }

        private static readonly Dictionary<AssetTypes, string[]> assetTypeToExtensions = new()
        {
            { AssetTypes.Scenes, new[] { ".unity" } },
            { AssetTypes.Prefabs, new[] { ".prefab" } },
            { AssetTypes.ScriptableObjects, new[] { ".asset" } },
            { AssetTypes.Materials, new[] { ".mat" } },
            { AssetTypes.Models, new[] { ".fbx", ".fbx.meta" } },
        };

        private static string cachedProjectPath;

        private static string ProjectPath
        {
            get
            {
                if (cachedProjectPath == null)
                {
                    string assetsFolder = Application.dataPath;
                    string projectFolder = assetsFolder.GetParentDirectory();

                    // Path up something like C:/Git/YourProjectName/ so it's unique to your checkout.
                    cachedProjectPath = projectFolder + "/";
                }

                return cachedProjectPath;
            }
        }

        private static string EditorPrefPrefix => ProjectPath + "RoyTheunissen/GUID-Search/";
        private static string EditorPrefSearchPath = EditorPrefPrefix + "SearchPath";
        private static string EditorPrefAssetsToSearchIn = EditorPrefPrefix + "AssetsToSearchIn";

        private const string MetaFileSuffix = ".meta";

        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private ObjectField assetField;
        private TextField guidField;
        private MaskField filesToSearchInField;
        private TextField pathField;
        private Button browseButton;
        private Button searchButton;
        private ListView resultsListView;

        private List<Object> results = new();
        
        private static Texture2D lightModeIcon;
        private static Texture2D darkModeIcon;

        private string SearchPath
        {
            get => EditorPrefs.GetString(EditorPrefSearchPath);
            set => EditorPrefs.SetString(EditorPrefSearchPath, value);
        }

        private int AssetsToSearchIn
        {
            get => EditorPrefs.GetInt(EditorPrefAssetsToSearchIn);
            set => EditorPrefs.SetInt(EditorPrefAssetsToSearchIn, value);
        }

        private bool HasFilesToSearchIn => EditorPrefs.HasKey(EditorPrefAssetsToSearchIn);

        [MenuItem("Window/Search/GUID Search")]
        public static void OpenGuidSearchWindow()
        {
            GuidSearchWindow window = GetWindow<GuidSearchWindow>();
            
            if (lightModeIcon == null)
                lightModeIcon = Resources.Load<Texture2D>("GuidSearchWindow Icon");

            if (darkModeIcon == null)
                darkModeIcon = Resources.Load<Texture2D>("d_GuidSearchWindow Icon");
            
            window.titleContent = new GUIContent(
                "GUID Search", EditorGUIUtility.isProSkin ? darkModeIcon : lightModeIcon);
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement tree = m_VisualTreeAsset.Instantiate();
            // HACK: Need to ensure that the instantiated visual tree grows to occupy the whole space, or specific elements
            // will not take up the space that they should be taking.
            tree.style.flexGrow = 1;
            tree.style.paddingLeft = 4;
            tree.style.paddingBottom = tree.style.paddingLeft;
            tree.style.paddingRight = tree.style.paddingLeft;
            tree.style.paddingTop = tree.style.paddingLeft;
            root.Add(tree);

            assetField = root.Q<ObjectField>("assetField");
            assetField.RegisterValueChangedCallback(HandleAssetFieldValueChangedCallback);

            guidField = root.Q<TextField>("guidField");
            guidField.SetEnabled(false);

            filesToSearchInField = root.Q<MaskField>();
            List<string> assetTypeNames = Enum.GetNames(typeof(AssetTypes)).ToList();
            filesToSearchInField.choices = assetTypeNames;
            if (!HasFilesToSearchIn)
                AssetsToSearchIn = (int)(AssetTypes.Prefabs | AssetTypes.Scenes);
            filesToSearchInField.value = AssetsToSearchIn;
            filesToSearchInField.RegisterValueChangedCallback(HandleFilesToSearchInChangedEvent);

            pathField = root.Q<TextField>("pathField");

            // Initialize the search path to the project path, if it's empty.
            if (string.IsNullOrEmpty(SearchPath))
                SearchPath = Application.dataPath;
            pathField.value = SearchPath;

            browseButton = root.Q<Button>("browseButton");
            browseButton.clicked += HandleBrowseButtonClicked;

            searchButton = root.Q<Button>("searchButton");
            searchButton.clicked += HandleSearchButtonClickedEvent;
            searchButton.SetEnabled(false);

            resultsListView = root.Q<ListView>("resultsListView");
            resultsListView.makeItem = () => new ObjectField();
            resultsListView.bindItem = (element, i) => (element as ObjectField).value = results[i];
            resultsListView.itemsSource = results;
            
            // Default to searching for whatever Object was selected when the window was opened.
            if (Selection.activeObject != null)
                assetField.value = Selection.activeObject;
        }

        private void HandleFilesToSearchInChangedEvent(ChangeEvent<int> evt)
        {
            AssetsToSearchIn = filesToSearchInField.value;
        }

        private void HandleBrowseButtonClicked()
        {
            string path = EditorUtility.OpenFolderPanel("Folder to search in", SearchPath, "");
            if (string.IsNullOrEmpty(path))
                return;

            SearchPath = path;
            pathField.value = SearchPath;
        }

        private void HandleAssetFieldValueChangedCallback(ChangeEvent<Object> evt)
        {
            Object asset = assetField.value;

            bool hasAsset = asset != null;

            searchButton.SetEnabled(hasAsset);

            if (!hasAsset)
            {
                guidField.value = string.Empty;
                return;
            }

            string assetPath = AssetDatabase.GetAssetPath(asset);
            string guid = AssetDatabase.AssetPathToGUID(assetPath);

            guidField.value = guid;
        }

        private void HandleSearchButtonClickedEvent()
        {
            // Find the file extensions for every asset type that's in the current asset type bitmask.
            List<string> extensions = new();
            AssetTypes[] assetTypeValues = (AssetTypes[])Enum.GetValues(typeof(AssetTypes));
            for (int i = 0; i < assetTypeValues.Length; i++)
            {
                if ((AssetsToSearchIn & (int)assetTypeValues[i]) == (int)assetTypeValues[i])
                    extensions.AddRange(assetTypeToExtensions[assetTypeValues[i]]);
            }

            // Find all fo the files of the specified extensions, in the specified folder, containing the specified GUID.
            string[] files = FindFilesContainingGuid(SearchPath, guidField.value, extensions.ToArray());
            results.Clear();
            for (int i = 0; i < files.Length; i++)
            {
                string assetPath = files[i].RemovePrefix(ProjectPath);

                // We cannot load a .meta file, load the file that the .meta file is for!
                assetPath = assetPath.RemoveSuffix(MetaFileSuffix);

                Object asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);

                if (asset == null)
                    continue;

                results.Add(asset);
            }

            resultsListView.RefreshItems();
        }

        private static string[] FindFilesContainingGuid(string folderPath, string guid, params string[] extensions)
        {
            List<string> results = new();

            for (int i = 0; i < extensions.Length; i++)
                results.AddRange(FindFilesContainingGuid(folderPath, guid, extensions[i]));

            return results.ToArray();
        }

        private static string[] FindFilesContainingGuid(string folderPath, string guid, string extension)
        {
            if (!Directory.Exists(folderPath))
                throw new DirectoryNotFoundException($"The folder path '{folderPath}' does not exist.");

            return Directory
                .EnumerateFiles(folderPath, "*" + extension, SearchOption.AllDirectories)
                .Where(file => File.ReadAllText(file).Contains(guid)).ToArray();
        }
    }
}
