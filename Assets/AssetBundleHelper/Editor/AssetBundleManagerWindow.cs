using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;

public class AssetBundleManagerWindow : EditorWindow {
	
	public List<FileInfo> detectedBundlesFileInfos;
	public List<AssetBundleListing> detectedBundles;
	
	[MenuItem("Window/AssetBundleManager %&5")]
	public static void Initialize(){
		AssetBundleManagerWindow window = EditorWindow.GetWindow(typeof(AssetBundleManagerWindow)) as AssetBundleManagerWindow;
		window.Refresh();
	}

	
	public void Refresh(){
		DirectoryInfo di = new DirectoryInfo(Application.dataPath);
		FileInfo[] files = di.GetFiles("*.asset", SearchOption.AllDirectories);
		detectedBundlesFileInfos = files.Where((x) => {
			string s = x.DirectoryName + Path.DirectorySeparatorChar+ x.Name;
            UnityEngine.Object o = AssetDatabase.LoadAssetAtPath(s.Substring(s.IndexOf("Assets" + Path.DirectorySeparatorChar)), typeof(AssetBundleListing)) as AssetBundleListing;
			return o != null;
		}).ToList();
		detectedBundles = detectedBundlesFileInfos.ConvertAll<AssetBundleListing>((x) => {
            string s = x.DirectoryName + Path.DirectorySeparatorChar + x.Name;
			return AssetDatabase.LoadAssetAtPath(s.Substring(s.IndexOf("Assets"+Path.DirectorySeparatorChar)), typeof(AssetBundleListing)) as AssetBundleListing;
		}).ToList();
		
	}

	public void OnGUI(){
		if(detectedBundlesFileInfos == null || detectedBundles == null){
			Refresh();
			return;
		}
		//EditorGUIUtility.LookLikeInspector();
		GUILayout.BeginHorizontal();
		GUILayout.Label("Bundles", EditorStyles.boldLabel);
		GUILayout.FlexibleSpace();
		if(GUILayout.Button("Refresh",EditorStyles.miniButton)){
			Refresh();
		}
		GUILayout.EndHorizontal();
		GUILayout.BeginVertical(GUI.skin.box);
		GUILayout.BeginHorizontal(EditorStyles.toolbar);
		GUILayout.Label("Name", GUILayout.MinWidth(100));
		GUILayout.FlexibleSpace();
		foreach(var plat in AssetBundleListingEditor.Settings.platforms){
			GUILayout.Label(new GUIContent(plat.name,plat.icon32),GUILayout.Height(14), GUILayout.Width(60));
		}
		GUILayout.EndHorizontal();
		
		List<AssetBundleListing> listingsOutOfDate = new List<AssetBundleListing>();
		var curPlats = AssetBundleListingEditor.Settings.GetPlatformsForCurrentBuildTarget(EditorUserBuildSettings.activeBuildTarget);
		
		for(int i =0; i < detectedBundles.Count; i++){
			AssetBundleListing listing = detectedBundles[i];
			if(listing == null){
				Refresh();
				return;
			}
			FileInfo listingFile = detectedBundlesFileInfos[i];
			if(listingFile == null){
				Refresh();
				return;
			}
			Dictionary<string, bool> isOutofdate = new Dictionary<string, bool>();
			GUILayout.BeginHorizontal();
			if(GUILayout.Button(listing.name,EditorStyles.miniButton,GUILayout.MinWidth(100))){
				Selection.activeObject = listing;
				EditorGUIUtility.PingObject(Selection.activeObject);
			}
			GUILayout.FlexibleSpace();
			DateTime badDate = new DateTime((System.Int64)0);
			foreach(var plat in AssetBundleListingEditor.Settings.platforms){
				DateTime lastBundleWriteTime = AssetBundleListingEditor.Settings.GetLastWriteTime(listing, plat.name);
				bool exists = lastBundleWriteTime != badDate;
				isOutofdate[plat.name] = listingFile.LastWriteTimeUtc > lastBundleWriteTime;
				var platObjs = listing.GetListingForPlatform(plat.name);
				
				string[] strings = platObjs.ConvertAll<string>((x) => { 
					return AssetDatabase.GetAssetPath(x);
				}).Distinct().ToArray<string>();
				strings = AssetDatabase.GetDependencies(strings);
				
				platObjs = Array.ConvertAll<string,UnityEngine.Object>(strings, (x) => {
					return AssetDatabase.LoadMainAssetAtPath(x);
				}).ToList();
				
				foreach(var obj in platObjs){
					string projectPath = AssetDatabase.GetAssetPath(obj);
					if(projectPath == ""){
						continue;
					}
					FileInfo objFileInfo = new FileInfo(projectPath);
					string metaPath = AssetDatabase.GetTextMetaDataPathFromAssetPath(projectPath);
					FileInfo metaFileInfo = new FileInfo(metaPath);
					if(objFileInfo.LastWriteTimeUtc > lastBundleWriteTime
						|| (metaPath != "" && metaFileInfo.LastWriteTimeUtc > lastBundleWriteTime)){
						isOutofdate[plat.name] = true;
					}
				}
				if(!exists){
					GUILayout.Label(AssetBundleListingEditor.Settings.box,GUILayout.Width(60));
					if(curPlats.Contains(plat) && !listingsOutOfDate.Contains(listing)){
						listingsOutOfDate.Add(listing);
					}
				}
				else if(isOutofdate[plat.name]){
					GUILayout.Label(AssetBundleListingEditor.Settings.outOfDate,GUILayout.Width(60));
					if(curPlats.Contains(plat) && !listingsOutOfDate.Contains(listing)){
						listingsOutOfDate.Add(listing);
					}					
				}
				else{
					GUILayout.Label(AssetBundleListingEditor.Settings.checkedBox,GUILayout.Width(60));
				}
			}
			GUILayout.EndHorizontal();
		}
		GUILayout.EndVertical();
		
		string platList = "";
		foreach(var plat in curPlats){
			platList += " "+plat.name;
		}
		if(listingsOutOfDate.Count > 0 &&  GUILayout.Button("Build missing/out of date bundles for"+platList + " ("+listingsOutOfDate.Count+")")){
			foreach(AssetBundleListing listing in listingsOutOfDate){
				AssetBundleListingEditor.BuildBundleForCurrentPlatforms(listing);
			}
		}
	}
}
