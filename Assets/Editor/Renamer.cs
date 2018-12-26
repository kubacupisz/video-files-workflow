using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Text.RegularExpressions;

public class Renamer : EditorWindow
{
	string m_Path;
	int    m_Hours;

	enum Mode
	{
		Rename,
		ShiftTime
	}

	Mode m_SelectedMode;

	[MenuItem("Tools/Renamer")]
	static void Init()
	{
		Renamer window = (Renamer)EditorWindow.GetWindow(typeof(Renamer));
		window.Show();
	}

	static DateTime ShiftCreationTime(string path, int hours)
	{
		DateTime dt = File.GetCreationTime (path);
		DateTime dtShifted = dt.AddHours (hours);
		Debug.Log ("File: " + path + ", creationTime: " + dt.ToString() + ", shiftedCreationTime: " + dtShifted);
		return dtShifted;
	}

	static string GetDateSuffix(string filename, Match match)
	{
		GroupCollection groups = match.Groups;
		Debug.Assert(groups.Count == 3);
		return groups [2].ToString();
	}

	void ShiftTimeGUI()
	{
		m_Hours = EditorGUILayout.IntField ("Hours", m_Hours);

		if (GUILayout.Button ("Dry run"))
		{
			string[] files = Directory.GetFiles(m_Path);
			Regex regex = new Regex(@"(\d{8}_\d{6}_)(.*)\..*");
			foreach (string filePath in files)
			{
				string extension = Path.GetExtension (filePath).ToLower();
				string filename = Path.GetFileName (filePath);
				Match match = regex.Match(filePath);
				if (match.Success)
				{
					DateTime dtShifted = ShiftCreationTime (filePath, m_Hours);
					string prefix = GetFileCreationDatePrefix (filePath, dtShifted);
					string suffix = GetDateSuffix(filename, match);
					string dstFilePath = prefix + "_" + suffix + extension;
					Debug.Log ("result: " + dstFilePath);
				}
			}
		}
	}

	void OnGUI()
	{
		// Mode tabs
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		m_SelectedMode = (Mode)GUILayout.Toolbar((int)m_SelectedMode, Styles.ModeToggles, Styles.ButtonStyle, GUI.ToolbarButtonSize.FitToContents);
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		EditorGUILayout.Space();

		// Source folder selection
		EditorGUILayout.BeginHorizontal ();
		EditorGUILayout.TextField("", m_Path);
		if (GUILayout.Button ("Edit..."))
		{
			string lastPath = EditorPrefs.GetString (kLastPathKey);
			m_Path = EditorUtility.OpenFolderPanel("Source folder", lastPath, "");
			if (m_Path.Length > 0)
				EditorPrefs.SetString (kLastPathKey, m_Path);
		}
		EditorGUILayout.EndHorizontal ();

		switch (m_SelectedMode)
		{
			case Mode.Rename:
				break;
			case Mode.ShiftTime:
				ShiftTimeGUI ();
				break;
		}
	}

	static string GetFileCreationDatePrefix(string filePath, DateTime dt)
	{
		string kFormat = "D2";
		string result =
			dt.Year + dt.Month.ToString (kFormat) + dt.Day.ToString (kFormat) +
			"_" +
			dt.Hour.ToString (kFormat) + dt.Minute.ToString (kFormat) + dt.Second.ToString (kFormat);

		return result;
	}

	static string ProcessGoProFirst(string filename, Match match)
	{
		GroupCollection groups = match.Groups;
		Debug.Assert(groups.Count == 3);
		return groups [2].ToString();
	}

	static string ProcessGoProNonFirst(string filename, Match match)
	{
		GroupCollection groups = match.Groups;
		Debug.Assert(groups.Count == 4);
		return groups[3].ToString() + "_" + groups [2].ToString();
	}

	const string kLastPathKey = "Renamer_LastPath";
	[MenuItem("Tools/Renamer - GoPro")]
	static void Apply()
	{
		string lastPath = EditorPrefs.GetString (kLastPathKey);
		string path = EditorUtility.OpenFolderPanel("Source folder", lastPath, "");
		if (path.Length == 0)
			return;

		EditorPrefs.SetString (kLastPathKey, path);

		string oldCurrentDirectory = Directory.GetCurrentDirectory ();
		Directory.SetCurrentDirectory (path);
		string[] files = Directory.GetFiles(path);

		Regex goProFirstRegex = new Regex(@"(GOPR)(\d*)");
		Regex goProNonFirstRegex = new Regex(@"(GP)(\d\d)(\d*)");
		foreach (string filePath in files)
		{
			string extension = Path.GetExtension (filePath).ToLower();
			string filename = Path.GetFileName (filePath);
			Debug.Log (filePath + ", filename: " + filename + ", ext: " + extension);

			// Delete thumbnails and low resolution video files
			if (extension == ".thm" || extension == ".lrv")
			{
				File.Delete (filePath);
				continue;
			}

			// Skip hidden files
			if (filename.StartsWith ("."))
				continue;

			DateTime dt = File.GetCreationTime (filePath);
			string prefix = GetFileCreationDatePrefix(filePath, dt);

			string suffix = "";
			Match goProFirstMatch = goProFirstRegex.Match(filename);
			Match goProNonFirstMatch = goProNonFirstRegex.Match(filename);
			if (goProFirstMatch.Success)
			{
				suffix = ProcessGoProFirst(filename, goProFirstMatch);
			}
			else if (goProNonFirstMatch.Success)
			{
				suffix = ProcessGoProNonFirst(filename, goProNonFirstMatch);
			}

			if (suffix.Length == 0)
			{
				Debug.LogError ("Empty suffix");
				continue;
			}

			string dstFilePath = prefix + "_" + suffix + extension;
			File.Move (filePath, dstFilePath);
			Debug.Log("result: " + dstFilePath);
		}

		Directory.SetCurrentDirectory (oldCurrentDirectory);
	}
}

static class Styles
{
	public static readonly GUIContent[] ModeToggles =
	{
		new GUIContent("Rename"),
		new GUIContent("Shift time")
	};

	public static readonly GUIStyle ButtonStyle = "LargeButton";
}