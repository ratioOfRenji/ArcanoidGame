using UnityEditor;
 
[InitializeOnLoad]
public class GameLoadEditorHook
{
	static GameLoadEditorHook()
	{
		PlayerSettings.GetPreloadedAssets();
	}
}