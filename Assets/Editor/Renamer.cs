using UnityEngine;
using UnityEditor;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NReco.VideoInfo;

public class Renamer : EditorWindow
{
	string m_Path;
	int    m_Hours = 0;
	int    m_Days = 0;
	int    m_Months = 0;
	string m_CustomSuffix = "";
	bool   m_DryRun = true;

	FileCreationSource m_FileCreationSource = FileCreationSource.FileModificationTime;
	
	const string kLastPathKey = "Renamer_LastPath";
	const string kFileDateTimePattern = @"(\d{8}_\d{6})\..*";
	const string kFileDateTimeNamePattern = @"(\d{8}_\d{6})_(.*)\..*";
	const string kTwoDigitsFormat = "D2";

	[MenuItem("Tools/Renamer")]
	static void Init()
	{
		Renamer window = (Renamer)EditorWindow.GetWindow(typeof(Renamer));
		window.Show();
	}

	void OnEnable()
	{
		m_Path = EditorPrefs.GetString (kLastPathKey);
	}

	enum FileNamePattern
	{
		DateTime,
		Img,
		GoProFirst,
		GoProNonFirst,
		Unknown
	}
	
	static string GetSuffix(string filePath, out FileNamePattern fnp)
	{
		{
			Regex regex = new Regex(kFileDateTimePattern);
			Match match = regex.Match(filePath);
			if (match.Success)
			{
				Debug.Assert(match.Groups.Count == 2);
				fnp = FileNamePattern.DateTime;
				return "0001";
			}
		}
		{
			Regex regex = new Regex(kFileDateTimeNamePattern);
			Match match = regex.Match(filePath);
			if (match.Success)
			{
				Debug.Assert(match.Groups.Count == 3);
				fnp = FileNamePattern.DateTime;
				return match.Groups[2].ToString();
			}
		}
		{
			Regex regex = new Regex(@"(IMG_)(.*)\..*");
			Match match = regex.Match(filePath);
			if (match.Success)
			{
				Debug.Assert(match.Groups.Count == 3);
				fnp = FileNamePattern.Img;
				return match.Groups[2].ToString();
			}
		}
		{
			Regex regex = new Regex(@"(GOPR)(\d*)");
			Match match = regex.Match(filePath);
			if (match.Success)
			{
				Debug.Assert(match.Groups.Count == 3);
				fnp = FileNamePattern.GoProFirst;
				return match.Groups[2].ToString();
			}
		}
		{
			Regex regex = new Regex(@"(GP)(\d\d)(\d*)");
			Match match = regex.Match(filePath);
			if (match.Success)
			{
				Debug.Assert(match.Groups.Count == 4);
				fnp = FileNamePattern.GoProNonFirst;
				return match.Groups[3].ToString() + "_" + match.Groups[2].ToString();
			}
		}
		{
			Regex regex = new Regex(@"(G[XH]01)(\d*)");
			Match match = regex.Match(filePath);
			if (match.Success)
			{
				Debug.Assert(match.Groups.Count == 3);
				fnp = FileNamePattern.GoProFirst;
				return match.Groups[2].ToString();
			}
		}
		{
			Regex regex = new Regex(@"(G[XH])(\d\d)(\d*)");
			Match match = regex.Match(filePath);
			if (match.Success)
			{
				Debug.Assert(match.Groups.Count == 4);
				fnp = FileNamePattern.GoProNonFirst;
				int id;
				if (Int32.TryParse(match.Groups[2].ToString(), out id))
				{
					id--;
					return match.Groups[3].ToString() + "_" + id.ToString(kTwoDigitsFormat);
				}
			}
		}
		{
			Regex regex = new Regex(@"(DJI_)(\d*)");
			Match match = regex.Match(filePath);
			if (match.Success)
			{
				Debug.Assert(match.Groups.Count == 3);
				fnp = FileNamePattern.GoProFirst;
				return match.Groups[2].ToString();
			}
		}

		fnp = FileNamePattern.Unknown;
		return "UNKNOWN";
	}

	static string CreateDryRunString(bool dryRun)
	{
		return (dryRun ? " [dryRun]" : "");
	}
	
	static void BuildPathAndMove(string filePath, DateTime dt, string customSuffix, bool dryRun)
	{
		string filename = Path.GetFileName(filePath);
		
		string prefix = CreateFileDateTimePrefix (dt);
		FileNamePattern fnp;
		string suffix = GetSuffix(filename, out fnp);
		string extension = Path.GetExtension(filePath)?.ToLower();
		
		string dstFilename = prefix + "_" + suffix + customSuffix + extension;


		string dryRunString = CreateDryRunString(dryRun);
		
		try
		{
			if (!dryRun)
				File.Move(filePath, dstFilename);
			
			Debug.Log("Moved file " + filePath + " to " + dstFilename + "." + dryRunString);
		}
		catch (IOException)
		{
			Debug.LogError ("Failed to move file " + filePath + " to " + dstFilename + " because the destination already existed."
				+ "Moving to the duplicates subfolder." + dryRunString);

			if (!dryRun)
			{
				const string kDuplicatesPath = "duplicates";
				try
				{
					Directory.CreateDirectory(kDuplicatesPath);
				}
				catch (Exception e)
				{
					Debug.LogError("Failed to created directory " + kDuplicatesPath + ": " + e.Message);
				}

				string dstFilePath = Path.Combine(kDuplicatesPath, dstFilename);
				try
				{
					File.Move(filePath, dstFilePath);
				}
				catch
				{
					Debug.LogError("Failed to move file " + filePath + " to " + dstFilePath + ".");
				}
			}
		}
	}

	static string GetVideoInfoString(string filePath)
	{
		StringBuilder sb = new StringBuilder();

		try
		{
			var ffProbe = new FFProbe();
			ffProbe.FFProbeExeName = "ffprobe";
			ffProbe.ToolPath = "/usr/local/bin";
			var videoInfo = ffProbe.GetMediaInfo(filePath);

			sb.AppendLine("Media information for: " + filePath);
			sb.AppendLine("File format: " + videoInfo.FormatName);
			sb.AppendLine("Duration: " + videoInfo.Duration);
			foreach (var tag in videoInfo.FormatTags) {
				sb.AppendLine($"\t{tag.Key}: {tag.Value}");
			}

			foreach (var stream in videoInfo.Streams) {
				sb.AppendLine($"Stream {stream.CodecName} ({stream.CodecType})");
				if (stream.CodecType == "video") {
					sb.AppendLine($"\tFrame size: {stream.Width}x{stream.Height}");
					sb.AppendLine($"\tFrame rate: {stream.FrameRate:0.##}");
				}
				foreach (var tag in stream.Tags) {
					sb.AppendLine($"\t{tag.Key}: {tag.Value}");
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogError(e);
		}

		return sb.ToString();
	}

	static string CreateFileDateTimePrefix(DateTime dt)
	{
		string result =
			dt.Year + dt.Month.ToString (kTwoDigitsFormat) + dt.Day.ToString (kTwoDigitsFormat) +
			"_" +
			dt.Hour.ToString (kTwoDigitsFormat) + dt.Minute.ToString (kTwoDigitsFormat) + dt.Second.ToString (kTwoDigitsFormat);

		return result;
	}

	enum FileCreationSource
	{
		FileModificationTime,
		VideoMetadata,
		FileDateTimeName
	}

	static DateTime GetDateTime(string filePath, FileCreationSource fcs)
	{
		DateTime dt = new DateTime();
		switch (fcs)
		{
			case FileCreationSource.FileModificationTime:
				dt = File.GetLastWriteTime(filePath);
				break;
			case FileCreationSource.VideoMetadata:
				GetVideoCreationTimeFromMetadata(filePath, out dt);
				break;
			case FileCreationSource.FileDateTimeName:
				Regex regex = new Regex(kFileDateTimeNamePattern);
				Match match = regex.Match(filePath);
				if (match.Success)
				{
					Debug.Assert(match.Groups.Count == 3);
					string dateTimePrefix = match.Groups[1].ToString();
					Debug.Log("dateTimePrefix before parsing: " + dateTimePrefix);
					dt = DateTime.ParseExact(dateTimePrefix, "yyyyMMdd'_'HHmmss", CultureInfo.InvariantCulture);
				}
				break;
			default:
				Debug.LogError("Unknown FileCreationSource.");
				break;
		}

		return dt;
	}

	static bool GetVideoCreationTimeFromMetadata(string filePath, out DateTime dt)
	{
		try
		{
			var ffProbe = new FFProbe();
			ffProbe.FFProbeExeName = "ffprobe";
			ffProbe.ToolPath = "/usr/local/bin";
			var videoInfo = ffProbe.GetMediaInfo(filePath);

			foreach (var tag in videoInfo.FormatTags) {
				if (tag.Key == "creation_time")
				{
					CultureInfo provider = CultureInfo.InvariantCulture;
					dt = DateTime.Parse(tag.Value, provider).ToUniversalTime();
					dt = DateTime.SpecifyKind(dt, DateTimeKind.Local);
					return true;
				}
			}
		}
		catch (Exception e)
		{
			Debug.LogError(e);
		}

		dt = new DateTime();
		return false;
	}

	enum FilterResult
	{
		Delete,
		Process,
		Skip
	}

	static FilterResult Filter(string filePath)
	{
		string extension = Path.GetExtension (filePath).ToLower();
		string filename = Path.GetFileName (filePath);

		// Delete thumbnails and low resolution video files
		if (extension == ".thm" || extension == ".lrv")
		{
			return FilterResult.Delete;
		}

		// Skip hidden files
		if (filename.StartsWith("."))
			return FilterResult.Skip;

		return FilterResult.Process;
	}

	void ProcessFile(string filePath, int hours, int days, int months, bool dryRun)
	{
		DateTime dt = GetDateTime(filePath, m_FileCreationSource);
		
		DateTime dtShifted = dt.AddHours(hours).AddDays(days).AddMonths(months);
		
		if (!dryRun)
		{
			File.SetLastWriteTime(filePath, dtShifted);
			DateTime dtNew = File.GetLastWriteTime (filePath);
			Debug.Assert(dtShifted == dtNew, "Setting file modification time failed. Expected: "
			                                 + dtShifted.ToString() + ". Actual: " + dtNew.ToString() + ".");
		}
		Debug.Log ("File: " + filePath + ", sourceTime(" + m_FileCreationSource.ToString() + "): " + dt.ToString() + ", shiftedTime: " + dtShifted);
		
		BuildPathAndMove(filePath, dtShifted, m_CustomSuffix, m_DryRun);
	}
	void Go()
	{
		string dryRunString = CreateDryRunString(m_DryRun);
		
		string oldCurrentDirectory = Directory.GetCurrentDirectory ();
		Directory.SetCurrentDirectory (m_Path);

		foreach (string filePath in Directory.GetFiles(m_Path))
		{
			try
			{
				string extension = Path.GetExtension (filePath).ToLower();
				string filename = Path.GetFileName (filePath);
				
				FilterResult fr = Filter(filePath);
	
				switch (fr)
				{
					case FilterResult.Delete:
						Debug.Log("Deleting " + filePath + "." + dryRunString);
						if (!m_DryRun)
						{
							File.Delete(filePath);
						}
						break;
					case FilterResult.Process:
						Debug.Log ("Processing " + filePath + ", filename: " + filename + ", ext: " + extension);
						Debug.Log(GetVideoInfoString(filePath));
						ProcessFile(filePath, m_Hours, m_Days, m_Months, m_DryRun);
						break;
					default:
						Debug.Log("Skipping " + filePath + ".");
						continue;
				}
			}
			catch (Exception e)
			{
				Debug.LogError(e);
			}
		}

		Directory.SetCurrentDirectory (oldCurrentDirectory);
	}
	
	void OnGUI()
	{
		// Mode tabs
		EditorGUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		m_FileCreationSource = (FileCreationSource)GUILayout.Toolbar((int)m_FileCreationSource, Styles.ModeToggles, Styles.ButtonStyle, GUI.ToolbarButtonSize.FitToContents);
		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		
		EditorGUILayout.Space();

		// Source folder selection
		EditorGUILayout.BeginHorizontal ();
		EditorGUILayout.TextField("", m_Path);
		if (GUILayout.Button ("Edit..."))
		{
			m_Path = EditorUtility.OpenFolderPanel("Source folder", m_Path, "");
			if (m_Path.Length > 0)
				EditorPrefs.SetString (kLastPathKey, m_Path);
		}
		EditorGUILayout.EndHorizontal ();
		
		EditorGUILayout.Space();
		
		m_Hours = EditorGUILayout.IntField ("Hours", m_Hours);
		m_Days = EditorGUILayout.IntField ("Days", m_Days);
		m_Months = EditorGUILayout.IntField ("Months", m_Months);
		m_CustomSuffix = EditorGUILayout.TextField("Custom suffix", m_CustomSuffix);
		m_DryRun = EditorGUILayout.Toggle("Dry Run", m_DryRun);

		if (GUILayout.Button("Go"))
		{
			Go();
		}
	}
}

static class Styles
{
	public static readonly GUIContent[] ModeToggles =
	{
		new GUIContent("File Modification Time"),
		new GUIContent("Video Metadata"),
		new GUIContent("File Date_Time Name"),
	};

	public static readonly GUIStyle ButtonStyle = "LargeButton";
}