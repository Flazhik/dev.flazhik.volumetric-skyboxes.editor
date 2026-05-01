using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using VolumetricSkyboxes;
using ColorField = UnityEditor.UIElements.ColorField;
using FloatField = UnityEngine.UIElements.FloatField;
using Object = UnityEngine.Object;

public class VolumetricSkyboxesExporter : EditorWindow
{
	private static SkyboxesExporterSettings DefaultExporterSetting {
		get
		{
			var exporterSettings = AssetDatabase.LoadAssetAtPath<SkyboxesExporterSettings>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/settings.asset");
			if (exporterSettings != null)
				return exporterSettings;
			
			exporterSettings = CreateInstance<SkyboxesExporterSettings>();
			AssetDatabase.CreateAsset(exporterSettings, "Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/settings.asset");

			return exporterSettings;
		}
	}
	
	[MenuItem("Skyboxes/Skyboxes menu", false, 0)]
	public static void ShowSkyboxes()
	{
		var wnd = GetWindow<VolumetricSkyboxesExporter>();
		wnd.titleContent = new GUIContent("Volumetric Skyboxes");
	}
	
	private static VisualTreeAsset skyboxesWindow;
	private static VisualTreeAsset skyboxEntry;
	private static VisualTreeAsset skyboxModifyWindow;
	private static VisualTreeAsset skyboxCreateWindow;
	private static VisualTreeAsset settingsWindow;
	private static StyleSheet generalStyleSheet;
	private static StyleSheet lightTheme;
	private static StyleSheet darkTheme;
	
	public void CreateGUI()
	{
		skyboxesWindow = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/SkyboxesMenu.uxml");
		skyboxEntry = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/SkyboxEntry.uxml");
		skyboxModifyWindow = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/SkyboxSettings.uxml");
		skyboxCreateWindow = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/CreateSkybox.uxml");
		settingsWindow = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/SettingsWindow.uxml");
		lightTheme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/LightTheme.uss");
		darkTheme = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/DarkTheme.uss");
		generalStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/General.uss");

		rootVisualElement.styleSheets.Add(EditorGUIUtility.isProSkin ? darkTheme : lightTheme);
		rootVisualElement.styleSheets.Add(generalStyleSheet);
		
		CreateAddressableSettingsIfMissing();
		DisplaySkyboxesWindow();
	}

	private static void CreateAddressableSettingsIfMissing()
	{
		if (AddressableAssetSettingsDefaultObject.Settings == null)
		{
				AddressableAssetSettingsDefaultObject.Settings = AddressableAssetSettings.Create(
					AddressableAssetSettingsDefaultObject.kDefaultConfigFolder,
					AddressableAssetSettingsDefaultObject.kDefaultConfigAssetName, true, true);
		}
	}
	
	private class SkyboxEntry
	{
		public readonly VisualElement root;
		public readonly Label skyboxTitle;
		public readonly Label skyboxAuthor;
		public readonly VisualElement icon;
		public readonly VisualElement controlContainer;
		public readonly Button deleteButton;
		public readonly Button modifyButton;
		public readonly Button exportButton;

		public SkyboxEntry(VisualElement target)
		{
			var tempContainer = skyboxEntry.CloneTree();

			root = tempContainer.contentContainer[0];
			target.Add(root);

			skyboxTitle = root.Q<Label>("skybox-title");
			skyboxAuthor = root.Q<Label>("skybox-author");
			icon = root.Q("icon");
			controlContainer = root.Q("control-container");
			deleteButton = root.Q<Button>("delete-skybox");
			modifyButton = root.Q<Button>("modify-skybox");
			exportButton = root.Q<Button>("export-skybox");

			controlContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
			modifyButton.RegisterCallback<MouseEnterEvent>(e =>
			{
				controlContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.Flex);
			});
			modifyButton.RegisterCallback<MouseLeaveEvent>(e =>
			{
				controlContainer.style.display = new StyleEnum<DisplayStyle>(DisplayStyle.None);
			});
		}
	}
	
	private class CreateSkyboxWindow
	{
		public readonly VisualElement root;
		public readonly TextField skyboxTitle;
		public readonly TextField author;
		public readonly ObjectField icon;
		public readonly Button createSkybox;
		public readonly Button cancelBottom;
		public readonly Button cancelTop;

		public CreateSkyboxWindow(VisualElement target)
		{
			root = skyboxCreateWindow.CloneTree().contentContainer[0];
			target.Add(root);
			
			skyboxTitle = root.Q<TextField>("skybox-title");
			author = root.Q<TextField>("skybox-author");
			icon = root.Q<ObjectField>("skybox-icon");
			icon.objectType = typeof(Sprite);
			createSkybox = root.Q<Button>("create-button");
			cancelBottom = root.Q<Button>("cancel-bottom");
			cancelTop = root.Q<Button>("cancel-top");
		}
	}
	
	private class SkyboxModifyWindow
	{
		public readonly VisualElement root;

        public readonly TextField skyboxTitle;
		public readonly TextField author;
        public readonly ObjectField icon;
        public readonly ObjectField skyboxMaterial;
        public readonly ObjectField prefab;
        public readonly Toggle customLighting;
        public readonly ColorField lightingColor;
        public readonly Slider lightingIntensity;
        
        public readonly ObjectField baseGridTexture;
        public readonly ObjectField topRowGridTexture;
        public readonly ObjectField topGridTexture;
        
        public readonly Toggle customFog;
        public readonly ColorField fogColor;
        public readonly FloatField fogMin;
        public readonly FloatField fogMax;
        public readonly FloatField parallax;
        public readonly FloatField nearClip;
        public readonly FloatField farClip;

		public readonly Label skyboxTitlePreview;
        public readonly VisualElement iconPreview;

        public readonly Button goBack;
        public readonly Button open;
        public readonly Button openFolder;

		public SkyboxModifyWindow(VisualElement target)
		{
			root = skyboxModifyWindow.CloneTree().contentContainer[0];
			target.Add(root);

            skyboxTitle = root.Q<TextField>("skybox-title");
            skyboxTitle.isDelayed = true;
			author = root.Q<TextField>("skybox-author");
            author.isDelayed = true;
            icon = root.Q<ObjectField>("icon");
            icon.objectType = typeof(Sprite);
            skyboxMaterial = root.Q<ObjectField>("skybox-material");
            skyboxMaterial.objectType = typeof(Material);
            prefab = root.Q<ObjectField>("prefab");
            prefab.objectType = typeof(GameObject);
            
            customLighting = root.Q<Toggle>("use-custom-lighting");
            lightingColor = root.Q<ColorField>("lighting-color");
            lightingIntensity = root.Q<Slider>("lighting-intensity");
            
            skyboxMaterial = root.Q<ObjectField>("skybox-material");
            skyboxMaterial.objectType = typeof(Material);
            
            customFog = root.Q<Toggle>("use-custom-fog");
            fogColor = root.Q<ColorField>("fog-color");
            fogMin = root.Q<FloatField>("fog-minimum");
            fogMin.isDelayed = true;
            fogMax = root.Q<FloatField>("fog-maximum");
            fogMax.isDelayed = true;
            
            baseGridTexture = root.Q<ObjectField>("base-grid-texture");
            baseGridTexture.objectType = typeof(Texture2D);
            topRowGridTexture = root.Q<ObjectField>("top-row-grid-texture");
            topRowGridTexture.objectType = typeof(Texture2D);
            topGridTexture = root.Q<ObjectField>("top-grid-texture");
            topGridTexture.objectType = typeof(Texture2D);
            
            parallax = root.Q<FloatField>("parallax-coefficient");
            parallax.isDelayed = true;
            nearClip = root.Q<FloatField>("near-clip-plane");
            nearClip.isDelayed = true;
            farClip = root.Q<FloatField>("far-clip-plane");
            farClip.isDelayed = true;

            skyboxTitlePreview = root.Q<Label>("skybox-title-preview");
            iconPreview = root.Q("icon-preview");

            goBack = root.Q<Button>("go-back");
            open = root.Q<Button>("edit-prefab");
            openFolder = root.Q<Button>("open-folder");
		}
	}
	
	private class SettingsWindow
	{
		public readonly VisualElement root;
		public readonly Button goBack;

		public readonly TextField output;
		public readonly Button openOutput;

		public SettingsWindow(VisualElement target)
		{
			root = settingsWindow.CloneTree().contentContainer[0];
			target.Add(root);

			goBack = root.Q<Button>("go-back");

			output = root.Q<TextField>("output");
			output.isDelayed = true;
			openOutput = root.Q<Button>("open-output");
		}
	}
	
	private void DisplaySkyboxesWindow()
    {
		var exporterSettings = DefaultExporterSetting;

        RemoveCurrentWindow();
        var window = skyboxesWindow.CloneTree()[0];
        rootVisualElement.Add(window);
        var list = window.Q("skybox-container");

        void RefreshSkyboxes()
        {
            if (window?.parent == null)
            {
                Undo.undoRedoPerformed -= RefreshSkyboxes;
                return;
			}

			if (!AssetDatabase.IsValidFolder("Assets/Skyboxes"))
				AssetDatabase.CreateFolder("Assets", "Skyboxes");

            while (list.childCount != 0)
                list.RemoveAt(0);

			foreach (var skyboxFolderPath in AssetDatabase.GetSubFolders("Assets/Skyboxes"))
			{
				var guid = AssetDatabase.AssetPathToGUID(skyboxFolderPath);
				var skybox = new SkyboxFolderAsset(guid);

				var skyboxElement = new SkyboxEntry(list)
				{
					skyboxTitle =
					{
						text = RemoveTags(skybox.skyboxData.title)
					},
					skyboxAuthor =
					{
						text = RemoveTags(skybox.skyboxData.author)
					}
				};

				if (skybox.skyboxData.skyboxIcon != null)
					skyboxElement.icon.style.backgroundImage = new StyleBackground(skybox.skyboxData.skyboxIcon.texture);

				skyboxElement.modifyButton.clicked += () =>
				{
					Undo.undoRedoPerformed -= RefreshSkyboxes;
					DisplaySkyboxModifyWindow(guid);
				};

                skyboxElement.deleteButton.clicked += () =>
                {
                    if (!EditorUtility.DisplayDialog("Warning", $"Do you want to delete skybox '{skybox.skyboxData.title}'?", "Yes", "No"))
                        return;

                    var folderPath = AssetDatabase.GUIDToAssetPath(skybox.folderGuid);
                    if (!string.IsNullOrEmpty(folderPath))
                    {
                        if (AssetDatabase.DeleteAsset(folderPath))
                            EditorUtility.DisplayDialog("Skyboxes Exporter", "Skybox has been removed", "Close");
                        else
							EditorUtility.DisplayDialog("Error", "Failed to remove a skybox", "Close");
					}

                    RefreshSkyboxes();
                };

                skyboxElement.exportButton.clicked += () => InternalExporter.TryExport(skybox, exporterSettings);
            }
        }

        Undo.undoRedoPerformed += RefreshSkyboxes;
        RefreshSkyboxes();

		var createSkyboxButton = window.Q<Button>("skybox-add");
        createSkyboxButton.clicked += () =>
        {
			Undo.undoRedoPerformed -= RefreshSkyboxes;
			DisplaySkyboxCreateWindow();
        };

		var refreshButton = window.Q<Button>("refresh");
		refreshButton.clicked += RefreshSkyboxes;

		var settingsButton = window.Q<Button>("settings");
		settingsButton.clicked += () => DisplaySettingsWindow(false);
    }
	
	private void DisplaySettingsWindow(bool indicateOutput)
    {
	    var exporterSettings = DefaultExporterSetting;

		RemoveCurrentWindow();
		var window = new SettingsWindow(rootVisualElement);

		if (indicateOutput)
			window.output.ElementAt(0).style.color = new StyleColor(Color.red);

		window.goBack.clicked += DisplaySkyboxesWindow;
		
		window.output.SetValueWithoutNotify(exporterSettings.buildPath);
		window.output.RegisterValueChangedCallback((e) =>
		{
			if (e.previousValue == e.newValue)
				return;

			Undo.RecordObject(exporterSettings, "Changed Output Path");
			exporterSettings.buildPath = e.newValue;
			EditorUtility.SetDirty(exporterSettings);
		});

		void ReloadUI()
		{
			if (window?.root?.parent == null)
			{
				Undo.undoRedoPerformed -= ReloadUI;
				return;
			}

			window.output.SetValueWithoutNotify(exporterSettings.buildPath);
		}

		Undo.undoRedoPerformed += ReloadUI;
		window.openOutput.clicked += () =>
		{
			var defaultPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
			var newOutput = EditorUtility.OpenFolderPanel("Open output path", defaultPath, "VolumetricSkyboxes");
			if (string.IsNullOrEmpty(newOutput))
				return;
			
			Undo.RecordObject(exporterSettings, "Changed Output Path");
			exporterSettings.buildPath = newOutput;
			EditorUtility.SetDirty(exporterSettings);
			window.output.SetValueWithoutNotify(newOutput);
		};
    }
	
	private void DisplaySkyboxCreateWindow()
    {
        RemoveCurrentWindow();
        var window = new CreateSkyboxWindow(rootVisualElement);

        window.cancelTop.clicked += DisplaySkyboxesWindow;
        window.cancelBottom.clicked += DisplaySkyboxesWindow;
        
        window.createSkybox.clicked += () =>
        {
            var folderName = RemoveTags(window.skyboxTitle.value);
            if (string.IsNullOrEmpty(folderName))
            {
                folderName = window.skyboxTitle.value;
                if (string.IsNullOrEmpty(folderName))
                    folderName = "Unknown skybox";
            }
            
            var newFolderName = folderName;
            var i = 1;
            while (AssetDatabase.IsValidFolder($"Assets/Skyboxes/{newFolderName}"))
	            newFolderName = $"{folderName} {i++}";

            folderName = newFolderName;
            var skyboxFolder = $"Assets/Skyboxes/{folderName}";
            var folderGuid = AssetDatabase.CreateFolder("Assets/Skyboxes", folderName);  

			var skyboxData = CreateInstance<VolumetricSkyboxData>();

            skyboxData.title = window.skyboxTitle.value;
            if (string.IsNullOrEmpty(skyboxData.title))
                skyboxData.title = "Unknown skybox";
            skyboxData.author = window.author.value;
            if (string.IsNullOrEmpty(skyboxData.author))
                skyboxData.author = "Unknown";
            if (window.icon.value != null)
				skyboxData.skyboxIcon = (Sprite)window.icon.value;

            AssetDatabase.CreateAsset(skyboxData, $"{skyboxFolder}/skyboxData.asset");
            GeneratePrefab(skyboxData, new SkyboxFolderAsset(folderGuid));
            DisplaySkyboxModifyWindow(AssetDatabase.AssetPathToGUID(skyboxFolder));
        };
    }
	
	private void DisplaySkyboxModifyWindow(string skyboxFolderGuid)
	{
		if (string.IsNullOrEmpty(skyboxFolderGuid))
			return;
		
		var folder = new SkyboxFolderAsset(skyboxFolderGuid);
        if (folder.skyboxData == null)
	        return;

        var skyboxData = folder.skyboxData;

        folder.Refresh();
        RemoveCurrentWindow();
        var window = new SkyboxModifyWindow(rootVisualElement);
		var originalLabelColor = window.skyboxTitle.labelElement.style.color;
		var highlightedLabelColor = new StyleColor(Color.yellow);

		void RefreshUI()
        {
            if (window?.root?.parent == null)
            {
				Undo.undoRedoPerformed -= RefreshUI;
                return;
			}
            
			window.skyboxTitlePreview.text = RemoveTags(skyboxData.title);
            window.iconPreview.style.backgroundImage = skyboxData.skyboxIcon == null
	            ? new StyleBackground()
	            : new StyleBackground(skyboxData.skyboxIcon.texture);

            window.skyboxTitle.SetValueWithoutNotify(skyboxData.title);
			window.skyboxTitle.labelElement.style.color = string.IsNullOrEmpty(skyboxData.title)
				? highlightedLabelColor
				: originalLabelColor;

			window.author.SetValueWithoutNotify(skyboxData.author);
            window.icon.SetValueWithoutNotify(skyboxData.skyboxIcon);
            window.skyboxMaterial.SetValueWithoutNotify(skyboxData.skyboxMaterial);
            window.prefab.SetValueWithoutNotify(skyboxData.prefab);
            
            window.customLighting.SetValueWithoutNotify(skyboxData.customLighting);
            window.lightingColor.SetValueWithoutNotify(skyboxData.lightingColor);
            window.lightingIntensity.SetValueWithoutNotify(skyboxData.lightingIntensity);
            
            window.customFog.SetValueWithoutNotify(skyboxData.customFog);
            window.fogColor.SetValueWithoutNotify(skyboxData.fogColor);
            window.fogMin.SetValueWithoutNotify(skyboxData.fogMinimum);
            window.fogMax.SetValueWithoutNotify(skyboxData.fogMaximum);
            
            window.baseGridTexture.SetValueWithoutNotify(skyboxData.baseGridTexture);
            window.topRowGridTexture.SetValueWithoutNotify(skyboxData.topRowGridTexture);
            window.topGridTexture.SetValueWithoutNotify(skyboxData.topGridTexture);
            
            window.parallax.SetValueWithoutNotify(skyboxData.ParallaxCoefficient);
            window.nearClip.SetValueWithoutNotify(skyboxData.NearClipPlane);
            window.farClip.SetValueWithoutNotify(skyboxData.FarClipPlane);

            if (skyboxData.customLighting)
            {
	            
            }
		}

        RefreshUI();
        Undo.undoRedoPerformed += RefreshUI;
        window.goBack.clicked += DisplaySkyboxesWindow;
        

        window.open.clicked += () =>
		{
			if (skyboxData.prefab == null)
			{
				if (!EditorUtility.DisplayDialog("Missing prefab", "Prefab is not set for the skybox. Do you want to create one now?", "Yes", "No"))
					return;

				GeneratePrefab(skyboxData, folder);
			}
			
			if (skyboxData.prefab == null)
			{
				EditorUtility.DisplayDialog("Error", "Failed to open scene", "Ok");
				return;
			}

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				return;

			PrefabStageUtility.OpenPrefab(AssetDatabase.GetAssetPath(skyboxData.prefab));
			RefreshUI();
		};
        
		window.openFolder.clicked += () =>
		{
			var folderPath = AssetDatabase.GUIDToAssetPath(skyboxFolderGuid);
			if (string.IsNullOrEmpty(folderPath))
				return;

			var folderObj = AssetDatabase.LoadAssetAtPath<Object>(folderPath);
			if (folderObj == null)
				return;

			EditorUtility.FocusProjectWindow();
			Selection.activeObject = folderObj;
			EditorGUIUtility.PingObject(folderObj);
		};
        
		window.skyboxTitle.RegisterValueChangedCallback(e =>
        {
            if (e.newValue == e.previousValue)
                return;
            
            Undo.RecordObject(skyboxData, "Change Skybox Title");
            skyboxData.title = e.newValue;
            window.skyboxTitlePreview.text = RemoveTags(e.newValue);
            
            EditorUtility.SetDirty(skyboxData);
            RefreshUI();
        });

        window.author.RegisterValueChangedCallback(e =>
        {
            if (e.previousValue == e.newValue)
                return;

            Undo.RecordObject(skyboxData, "Change Skybox Author");
            skyboxData.author = e.newValue;
            EditorUtility.SetDirty(skyboxData);

            RefreshUI();
        });

		window.icon.RegisterValueChangedCallback(e =>
		{
			if (e.previousValue == e.newValue)
				return;

            var newValue = e.newValue == null ? null : (Sprite)e.newValue;
			window.iconPreview.style.backgroundImage = newValue == null
				? new StyleBackground()
				: new StyleBackground(newValue.texture);

			Undo.RecordObject(skyboxData, "Change Skybox Thumbnail");
			skyboxData.skyboxIcon = newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.skyboxMaterial.RegisterValueChangedCallback(e =>
		{
			if (e.previousValue == e.newValue)
				return;

			var newValue = e.newValue == null ? null : (Material)e.newValue;
			
			Undo.RecordObject(skyboxData, "Change Skybox Material");
			skyboxData.skyboxMaterial = newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.prefab.RegisterValueChangedCallback(e =>
		{
			var newValue = e.newValue == null ? null : (GameObject)e.newValue;
			Undo.RecordObject(skyboxData, "Change Prefab");
			skyboxData.prefab = newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.customLighting.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Enable Custom Lighting");
			skyboxData.customLighting = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.lightingColor.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Change Lighting Color");
			skyboxData.lightingColor = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.lightingIntensity.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Change Lighting Intensity");
			skyboxData.lightingIntensity = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.customFog.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Enable Custom Fog");
			skyboxData.customFog = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.fogColor.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Change Fog Color");
			skyboxData.fogColor = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.fogMin.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Change Fog Minimum");
			skyboxData.fogMinimum = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.fogMax.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Change Fog Maximum");
			skyboxData.fogMaximum = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.baseGridTexture.RegisterValueChangedCallback(e =>
		{
			if (e.previousValue == e.newValue)
				return;

			var newValue = e.newValue == null ? null : (Texture2D)e.newValue;
			
			Undo.RecordObject(skyboxData, "Change Base Grid Texture");
			skyboxData.baseGridTexture = newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.topRowGridTexture.RegisterValueChangedCallback(e =>
		{
			if (e.previousValue == e.newValue)
				return;

			var newValue = e.newValue == null ? null : (Texture2D)e.newValue;
			
			Undo.RecordObject(skyboxData, "Change Top Row Grid Texture");
			skyboxData.topRowGridTexture = newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.topGridTexture.RegisterValueChangedCallback(e =>
		{
			if (e.previousValue == e.newValue)
				return;

			var newValue = e.newValue == null ? null : (Texture2D)e.newValue;
			
			Undo.RecordObject(skyboxData, "Change Top Grid Texture");
			skyboxData.topGridTexture = newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.parallax.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Change Parallax Coefficient");
			skyboxData.ParallaxCoefficient = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.nearClip.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Change Near Clip Plane");
			skyboxData.NearClipPlane = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
		
		window.farClip.RegisterValueChangedCallback(e =>
		{
			Undo.RecordObject(skyboxData, "Change Far Clip Plane");
			skyboxData.FarClipPlane = e.newValue;
			EditorUtility.SetDirty(skyboxData);
		});
	}
	
	private void RemoveCurrentWindow()
	{
		while (rootVisualElement.childCount != 0)
			rootVisualElement.RemoveAt(0);
	}
	
	private class SkyboxFolderAsset
	{
		public readonly string folderGuid;
		public string skyboxGuid { get; private set; }

		public VolumetricSkyboxData skyboxData;

		public void Refresh()
		{
			skyboxData = null;

			if (string.IsNullOrEmpty(folderGuid))
				return;

			var path = AssetDatabase.GUIDToAssetPath(folderGuid);
			if (string.IsNullOrEmpty(path))
				return;

			var files = Directory.GetFiles(path);
			skyboxData = files
				.Where(pth => AssetDatabase.GetMainAssetTypeAtPath(pth) == typeof(VolumetricSkyboxData))
				.Select(AssetDatabase.LoadAssetAtPath<VolumetricSkyboxData>)
				.FirstOrDefault();

			if (skyboxData == null)
			{
				skyboxData = CreateInstance<VolumetricSkyboxData>();
				skyboxData.name = "Unnamed Skybox";
				skyboxData.author = "Unknown";
				skyboxData.ParallaxCoefficient = 1f;
				skyboxData.NearClipPlane = 0.1f;
				skyboxData.FarClipPlane = 4000f;
				
				AssetDatabase.CreateAsset(skyboxData, AssetDatabase.GenerateUniqueAssetPath($"{path}/skyboxData.asset"));
			}

			skyboxGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skyboxData));
		}

		public SkyboxFolderAsset(string folderGuid)
		{
			this.folderGuid = folderGuid;
			Refresh();
		}

		public string GetHash(bool progressBar = false)
		{
			Refresh();
			if (skyboxData == null)
				return "b19b00b5000000000000000000000000";

			var hash = MD5.Create();

			var folderPath = AssetDatabase.GUIDToAssetPath(folderGuid);
			foreach (var file in GetFilesRecursive(folderPath).OrderBy(e => e))
			{
				var fileInfo = new FileInfo(file);
				if (!fileInfo.Exists)
					continue;

				hash.TransformBlock(BitConverter.GetBytes(fileInfo.LastWriteTime.ToFileTime()), 0, 8, null, 0);
			}

			hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			var finalHashArr = hash.Hash;
			var finalHash = BitConverter.ToString(finalHashArr).Replace("-", "").ToLower();

			if (progressBar)
				EditorUtility.ClearProgressBar();
			return finalHash;
		}
	}

	private static class InternalExporter
	{
		public class SkyboxData
		{
			public string skyboxName { get; set; }
			public string author { get; set; }
			public string guid { get; set; }
			public string buildHash { get; set; }
			public int version { get; set; }
			public string dataPath { get; set; }
			public string prefabPath { get; set; }
			public string skyboxPath { get; set; }
		}
		
		private static string[] builtInGroupNames = {
			"Built In Data",
			"Default Group",
			"Assets",
			"Other",
			"Music"
		};

		public static void TryExport(SkyboxFolderAsset skybox, SkyboxesExporterSettings exporterSettings)
		{
			skybox.Refresh();
			if (!Directory.Exists(exporterSettings.buildPath))
			{
				EditorUtility.DisplayDialog("Error", "Set the skyboxes output directory first.", "Close");
				var wnd = GetWindow<VolumetricSkyboxesExporter>();
				wnd.titleContent = new GUIContent("Volumetric Skyboxes");
				wnd.DisplaySettingsWindow(true);
				return;
			}
			
			BuildSkybox(skybox, exporterSettings);
		}

		public static void BuildSkybox(SkyboxFolderAsset skybox, SkyboxesExporterSettings exporterSettings)
		{
			var destination = exporterSettings.buildPath;

			if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
				return;
			
			skybox.Refresh();
			var currentBuildHash = skybox.GetHash(progressBar: true);

			var settings = AddressableAssetSettingsDefaultObject.Settings;
			var templateSchema = (BundledAssetGroupSchema)AssetDatabase
				.LoadAssetAtPath<AddressableAssetGroupTemplate>(
					"Assets/AddressableAssetsData/AssetGroupTemplates/Packed Assets.asset")
				.GetSchemaByType(typeof(BundledAssetGroupSchema));
			
			var skyboxGroup = MakeAddressable(skybox);
			var defGroup = settings.DefaultGroup;

			void ProcessBuiltInSchema(BundledAssetGroupSchema schema)
			{
				schema.IncludeInBuild = true;
				schema.Compression = BundledAssetGroupSchema.BundleCompressionMode.LZ4;
				schema.BuildPath.SetVariableByName(settings, "Local.BuildPath");
				schema.LoadPath.SetVariableByName(settings, "Local.LoadPath");
				schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
				schema.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid;
				schema.UseAssetBundleCrcForCachedBundles = false;
				schema.UseAssetBundleCrc = false;
			}

			void ProcessCustomSchema(BundledAssetGroupSchema schema)
			{
				if (schema.Group == defGroup)
					return;

				schema.IncludeInBuild = false;
				schema.IncludeAddressInCatalog = true;
				schema.BuildPath.SetVariableByName(settings, "Remote.BuildPath");
				schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
				schema.BundleNaming = BundledAssetGroupSchema.BundleNamingStyle.NoHash;
				schema.InternalBundleIdMode = BundledAssetGroupSchema.BundleInternalIdMode.GroupGuid;
				schema.InternalIdNamingMode = BundledAssetGroupSchema.AssetNamingMode.GUID;
				schema.UseAssetBundleCrcForCachedBundles = false;
				schema.UseAssetBundleCrc = false;
			}

			foreach (var entry in defGroup.entries.ToArray())
				settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(entry.AssetPath));

			ProcessBuiltInSchema(GetOrCreateSchemaFromTemplate(defGroup, templateSchema));
			GetOrCreateSchemaFromTemplate(defGroup, templateSchema).InternalBundleIdMode =
				BundledAssetGroupSchema.BundleInternalIdMode.GroupGuidProjectIdHash;

			foreach (var builtInGroup in settings.groups.Where(g => builtInGroupNames.Contains(g.Name)))
			{
				if (builtInGroup.GetSchema<PlayerDataGroupSchema>() != null)
					continue;

				ProcessBuiltInSchema(GetOrCreateSchemaFromTemplate(builtInGroup, templateSchema));
			}

			foreach (var customGroup in settings.groups.Where(g => !builtInGroupNames.Contains(g.Name)))
			{
				var schema = GetOrCreateSchemaFromTemplate(customGroup, templateSchema);
				ProcessCustomSchema(schema);

				if (schema.Group == skyboxGroup)
					schema.IncludeInBuild = true;
			}

			settings.MonoScriptBundleCustomNaming = skyboxGroup.Guid.Substring(0, 16);
			settings.profileSettings.SetValue(settings.activeProfileId, "Remote.BuildPath", "BuiltBundles");
			settings.profileSettings.SetValue(settings.activeProfileId, "Remote.LoadPath",
				@"{UnpackedVolumetricSkyboxesPath}\" + skyboxGroup.Guid);

			var indexOfBuilder = -1;
			for (var i = 0; i < settings.DataBuilders.Count; i++)
				if (settings.DataBuilders[i] is BuildScriptPackedMode)
				{
					indexOfBuilder = i;
					break;
				}

			if (indexOfBuilder == -1)
			{
				var asset = CreateInstance<BuildScriptPackedMode>();

				const string path = "Assets/AddressableAssetsData/DataBuilders";
				var fileName = "BuildScriptPackedMode.asset";
				var newName = fileName;
				var i = 0;
				while (!string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(Path.Combine(path, newName))))
					newName = fileName.Replace(".asset", $"_{i++}.asset");
				fileName = newName;

				var assetPath = Path.Combine(path, fileName);
				AssetDatabase.CreateAsset(asset, assetPath);

				settings.DataBuilders.Add(asset);
				indexOfBuilder = settings.DataBuilders.Count - 1;
			}

			settings.ActivePlayerDataBuilderIndex = indexOfBuilder;
			
			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
			
			AddressableAssetSettings.BuildPlayerContent(out var result);
			skybox.Refresh();
			
			if (result == null || !string.IsNullOrEmpty(result.Error))
			{
				EditorUtility.DisplayDialog("Error",
					"Encountered an error while building content. Please try to reopen the project. If the issue persists send error log",
					"Ok");
				return;
			}
			
			var tempBuildDir = Path.Combine(Application.dataPath, "../", "TempBundle");
			if (Directory.Exists(tempBuildDir))
				Directory.Delete(tempBuildDir, true);
			Directory.CreateDirectory(tempBuildDir);
			
			var monoPath = "";
			foreach (var builtBundle in result.AssetBundleBuildResults)
			{
				var realPath = builtBundle.FilePath.Substring(0, builtBundle.FilePath.Length - 40) + ".bundle";

				if (builtBundle.SourceAssetGroup != skyboxGroup)
				{
					if (Path.GetFileName(builtBundle.FilePath).StartsWith(settings.MonoScriptBundleCustomNaming))
					{
						monoPath = realPath;
						File.Copy(monoPath, Path.Combine(tempBuildDir, Path.GetFileName(monoPath)));
					}

					continue;
				}

				File.Copy(realPath, Path.Combine(tempBuildDir, Path.GetFileName(realPath)));
			}
			
			var sourcePath = @"{UnityEngine.AddressableAssets.Addressables.RuntimePath}\\StandaloneWindows64\\" +
			                 Path.GetFileName(monoPath);
			var destinationPath = @"{UnpackedVolumetricSkyboxesPath}\\" + skyboxGroup.Guid + @"\\" +
			                      Path.GetFileName(monoPath);
			var catalog = File.ReadAllText(Path.Combine(result.OutputPath, "../", "catalog.json"));

			catalog = catalog.Replace(sourcePath, destinationPath);
			catalog = Regex.Replace(catalog, Regex.Escape(destinationPath) + "[0-9a-f]+_unitybuiltinshaders\\.bundle", sourcePath + "shader_unitybuiltinshaders.bundle");
			File.WriteAllText(Path.Combine(tempBuildDir, "catalog.json"), catalog);
			
			var bundleData = new SkyboxData
			{
				skyboxName = skybox.skyboxData.title,
				author = skybox.skyboxData.author,
				version = 1,
				buildHash = currentBuildHash,
				guid = skyboxGroup.Guid,
				dataPath = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skybox.skyboxData)),
				prefabPath = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skybox.skyboxData.prefab)),
				skyboxPath = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skybox.skyboxData.skyboxMaterial))
			};

			var levelIcon = skybox.skyboxData.skyboxIcon;
			if (levelIcon != null && levelIcon.texture != null)
			{
				Texture2D DuplicateTexture(Texture2D source)
				{
					var renderTex = RenderTexture.GetTemporary(
						source.width,
						source.height,
						0,
						RenderTextureFormat.Default,
						RenderTextureReadWrite.Linear);

					Graphics.Blit(source, renderTex);
					var previous = RenderTexture.active;
					RenderTexture.active = renderTex;
					var readableText = new Texture2D(source.width, source.height);
					readableText.ReadPixels(new Rect(0, 0, renderTex.width, renderTex.height), 0, 0);
					readableText.Apply();
					RenderTexture.active = previous;
					RenderTexture.ReleaseTemporary(renderTex);
					return readableText;
				}

				var sourceTexture = levelIcon.texture;
				var decompressedTexture = DuplicateTexture(sourceTexture);

				var pngBytes = ImageConversion.EncodeToPNG(decompressedTexture);
				DestroyImmediate(decompressedTexture);
				var iconDestinationPath = Path.Combine(tempBuildDir, "icon.png");
				File.WriteAllBytes(iconDestinationPath, pngBytes);
			}

			File.WriteAllText(Path.Combine(tempBuildDir, "data.json"), JsonConvert.SerializeObject(bundleData));

			// Find destination file
			var outputPath = "";
			foreach (var skyboxFile in GetFilesRecursive(destination).Where(f => f.EndsWith(".cgvsb")))
			{
				try
				{
					using var skyboxArchive = new ZipArchive(File.Open(skyboxFile, FileMode.Open, FileAccess.Read));
					var dataEntry = skyboxArchive.GetEntry("data.json");
					if (dataEntry == null)
						continue;

					using var dataStr = new StreamReader(dataEntry.Open());
					var data = JsonConvert.DeserializeObject<SkyboxData>(dataStr.ReadToEnd());
					if (data.guid != skyboxGroup.Guid)
						continue;
					
					outputPath = skyboxFile;
					break;
				}
				catch (Exception)
				{
					// ignored
				}
			}

			if (string.IsNullOrEmpty(outputPath))
			{
				var fileName = GetPathSafeName(skybox.skyboxData.title);
				var newFileName = fileName;
				var i = 0;
				while (File.Exists(Path.Combine(destination, $"{newFileName}.cgvsb")))
					newFileName = $"{fileName}_{i++}";

				outputPath = Path.Combine(destination, $"{newFileName}.cgvsb");
			}

			var zipDir = Path.Combine(tempBuildDir, $"{skyboxGroup.Name}.cgvsb");
			using (var archive = new ZipArchive(File.Open(zipDir, FileMode.Create, FileAccess.ReadWrite),
				       ZipArchiveMode.Create))
			{
				foreach (var file in Directory.GetFiles(tempBuildDir).Where(dir => dir != zipDir))
				{
					var entry = archive.CreateEntry(Path.GetFileName(file));
					using var fs = File.Open(file, FileMode.Open, FileAccess.Read);
					using var entryStream = entry.Open();
					fs.CopyTo(entryStream);
				}
			}

			File.Copy(zipDir, outputPath, true);
		}
		
		public static AddressableAssetGroup MakeAddressable(SkyboxFolderAsset skybox)
		{
			skybox.Refresh();
			var bundleGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(skybox.skyboxData));

			var settings = AddressableAssetSettingsDefaultObject.Settings;
			var bundleGroup = GetGroupByGUID(bundleGuid);
			if (bundleGroup == null)
				bundleGroup = CreateGroupByGUID(skybox.skyboxData.title, bundleGuid);

			foreach (var entry in bundleGroup.entries.ToArray())
				settings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(entry.AssetPath));

			var dataPath = AssetDatabase.GUIDToAssetPath(skybox.folderGuid);
			foreach (var asset in GetFilesRecursive(dataPath).Where(f => !f.EndsWith(".meta")))
			{
				var assetGuid = AssetDatabase.AssetPathToGUID(asset);
				var entry = settings.CreateOrMoveEntry(assetGuid, bundleGroup);
				entry.address = assetGuid;
			}

			return bundleGroup;
		}
		
		private static AddressableAssetGroup GetGroupByGUID(string guid)
		{
			var settings = AddressableAssetSettingsDefaultObject.Settings;
			return settings.groups.FirstOrDefault(group => group.Guid == guid);
		}

		private static FieldInfo AddressableAssetGroup_m_GUID = typeof(AddressableAssetGroup).GetField("m_GUID", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		private static AddressableAssetGroup CreateGroupByGUID(string groupName, string guid)
		{
			var group = GetGroupByGUID(guid);
			if (group != null)
				return group;

			var settings = AddressableAssetSettingsDefaultObject.Settings;
			var newGroupName = groupName;
			var i = 1;
			while (settings.groups.Any(g => g.Name == newGroupName))
				newGroupName = $"{groupName}_{i++}";
			groupName = newGroupName;

			group = settings.CreateGroup(groupName, false, false, false, new List<AddressableAssetGroupSchema>());
			AddressableAssetGroup_m_GUID.SetValue(group, guid);

			return group;
		}
	}

	private static void GeneratePrefab(VolumetricSkyboxData skyboxData, SkyboxFolderAsset folder)
	{
		var folderPath = AssetDatabase.GUIDToAssetPath(folder.folderGuid);
		var newPrefabPath = AssetDatabase.GenerateUniqueAssetPath($"{folderPath}/{GetPathSafeName(skyboxData.title)}.prefab");
		var templatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Packages/dev.flazhik.volumetric-skyboxes.editor/Editor/templatePrefab.prefab");

		if (templatePrefab == null)
			throw new Exception("Failed to find template prefab for the skybox");
				
		var tempPrefab = PrefabUtility.InstantiatePrefab(templatePrefab) as GameObject;
		try
		{
			PrefabUtility.SaveAsPrefabAsset(templatePrefab, newPrefabPath);
		}
		finally
		{
			DestroyImmediate(tempPrefab);
		}

		skyboxData.prefab = AssetDatabase.LoadAssetAtPath<GameObject>(newPrefabPath);
		EditorUtility.SetDirty(skyboxData);
	}
	
	private static T GetOrCreateSchemaFromTemplate<T>(AddressableAssetGroup group, T template, bool postEvents = true) where T : AddressableAssetGroupSchema
	{
		var schema = (T)group.GetSchema(typeof(T));
		if (schema == null)
			schema = (T)group.AddSchema(template, postEvents);

		return schema;
	}
		
	// Unity style paths starting with 'Asset/', given folder path should NOT end with a slash.
	private static IEnumerable<string> GetFilesRecursive(string folderPath)
	{
		foreach (var file in Directory.GetFiles(folderPath))
			yield return $"{folderPath}/{Path.GetFileName(file)}";

		foreach (var folder in Directory.GetDirectories(folderPath))
		foreach (var subFile in GetFilesRecursive($"{folderPath}/{Path.GetFileName(folder)}"))
			yield return subFile;
	}
	
	private static string RemoveTags(string richText) =>
		string.IsNullOrEmpty(richText) ? richText : new Regex(@"<[^>]*>").Replace(richText, string.Empty);
	
	
	private static string GetPathSafeName(string name)
	{
		var newName = new StringBuilder();
		for (var i = 0; i < name.Length; i++)
		{
			var c = name[i];

			if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
				newName.Append(c);
			else if (c == ' ' && i > 0 && name[i - 1] != ' ')
				newName.Append('_');
		}

		var finalName = newName.ToString();
		return string.IsNullOrEmpty(finalName)
			? "file"
			: finalName;
	}
}
	