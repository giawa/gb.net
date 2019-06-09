using ImGuiNET;
using System.Collections.Generic;
using System.Numerics;

namespace GB
{
    public struct FileInfoStruct
    {
        public char type;
        public string filePath;
        public string fileName;
        public string ext;
    };

    public class FileDialog
    {
        private List<FileInfoStruct> m_FileList;
        private string m_SelectedFileName;
        private string m_CurrentPath;
        private string[] m_CurrentPath_Decomposition;
        private string m_CurrentFilterExt;

        private static readonly uint MAX_FILE_DIALOG_NAME_BUFFER = 1024;
        //public static char[] FileNameBuffer = new char[MAX_FILE_DIALOG_NAME_BUFFER];
        private string FileNameBuffer;
        private int FilterIndex;

        public bool IsOk { get; private set; }

        public string FullPath { get; private set; }

        public FileDialog()
        {
            m_FileList = new List<FileInfoStruct>();
        }

        private void ScanDir(string path)
        {
            System.IO.DirectoryInfo directory = new System.IO.DirectoryInfo(path);

            m_FileList = new List<FileInfoStruct>();

            foreach (var dir in directory.GetDirectories())
            {
                m_FileList.Add(new FileInfoStruct()
                {
                    fileName = dir.Name,
                    filePath = dir.FullName,
                    type = 'd'
                });
            }

            foreach (var file in directory.GetFiles())
            {
                m_FileList.Add(new FileInfoStruct()
                {
                    fileName = file.Name,
                    filePath = file.FullName,
                    type = 'f'
                });
            }
        }

        private void ComposeNewPath(int pathIndex)
        {
            m_CurrentPath = "";

            for (int i = 0; i <= pathIndex; i++)
            {
                if (string.IsNullOrEmpty(m_CurrentPath)) m_CurrentPath = m_CurrentPath_Decomposition[i];
                else m_CurrentPath += "\\" + m_CurrentPath_Decomposition[i];
            }
        }

        private void DecomposePath()
        {
            m_CurrentPath_Decomposition = m_CurrentPath.Split(new char[] { '\\' });
            if (m_CurrentPath_Decomposition.Length == 2)
                if (m_CurrentPath_Decomposition[1] == "")
                    m_CurrentPath_Decomposition = new string[] { m_CurrentPath_Decomposition[0] };
        }

        public bool DisplayFileDialog(string vName, string[] vFilters, string vPath, string vDefaultFileName)
        {
            bool res = false;

            IsOk = false;

            ImGui.Begin(vName);

            if (string.IsNullOrEmpty(vPath)) vPath = ".";

            if (m_FileList.Count == 0)
            {
                if (vDefaultFileName.Length > 0)
                {
                    FileNameBuffer = vDefaultFileName;
                }

                ScanDir(vPath);
            }

            // provide some sane defaults if this has just initialized
            if (m_CurrentFilterExt == null && vFilters != null && vFilters.Length > 0) m_CurrentFilterExt = vFilters[0];
            if (m_CurrentPath == null) m_CurrentPath = vPath;
            if (m_CurrentPath_Decomposition == null) DecomposePath();

            // show current path
            bool pathClick = false;
            for (int i = 0; i < m_CurrentPath_Decomposition.Length; i++)
            {
                var itPathDecomp = m_CurrentPath_Decomposition[i];

                if (itPathDecomp != m_CurrentPath_Decomposition[0])
                    ImGui.SameLine();
                // every button needs a unique id, but no text after '##' is shown
                if (ImGui.Button(itPathDecomp + "##" + i.ToString()))
                {
                    ComposeNewPath(i);
                    pathClick = true;
                    break;
                }
            }

            Vector2 size = ImGui.GetContentRegionMax() - new Vector2(0.0f, 120.0f);

            ImGui.BeginChild("##FileDialog_FileList", size);

            foreach (var infos in m_FileList)
            {
                string str = "";
                if (infos.type == 'd') str = "[Dir] " + infos.fileName;
                if (infos.type == 'l') str = "[Link] " + infos.fileName;
                if (infos.type == 'f') str = "[File] " + infos.fileName;

                if (infos.type == 'f' && !string.IsNullOrEmpty(m_CurrentFilterExt) && !infos.fileName.EndsWith(m_CurrentFilterExt)) continue;

                if (ImGui.Selectable(str, (infos.fileName == m_SelectedFileName)))
                {
                    if (infos.type == 'd')
                    {
                        if (infos.fileName == "..")
                        {
                            if (m_CurrentPath_Decomposition.Length > 1)
                            {
                                ComposeNewPath(m_CurrentPath.Length - 2);
                            }
                        }
                        else
                        {
                            m_CurrentPath += "\\" + infos.fileName;
                        }
                        pathClick = true;
                    }
                    else
                    {
                        m_SelectedFileName = infos.fileName;
                        FileNameBuffer = m_SelectedFileName;
                        FullPath = infos.filePath;
                    }
                    break;
                }
            }

            if (pathClick == true)
            {
                ScanDir(m_CurrentPath);
                m_CurrentPath_Decomposition = m_CurrentPath.Split(new char[] { '\\' });
                if (m_CurrentPath_Decomposition.Length == 2)
                    if (m_CurrentPath_Decomposition[1] == "")
                        m_CurrentPath_Decomposition = new string[] { m_CurrentPath_Decomposition[0] };
            }

            ImGui.EndChild();

            ImGui.Text("File Name : ");

            ImGui.SameLine();

            float width = ImGui.GetContentRegionAvail().X;
            if (vFilters.Length != 0) width -= 120.0f;
            ImGui.PushItemWidth(width);
            ImGui.InputText("##FileName", ref FileNameBuffer, MAX_FILE_DIALOG_NAME_BUFFER, ImGuiInputTextFlags.ReadOnly);
            //ImGui.Text(FileNameBuffer);
            ImGui.PopItemWidth();

            int selected = 0;

            if (vFilters.Length != 0)
            {
                ImGui.SameLine();

                ImGui.PushItemWidth(100.0f);
                bool comboClick = ImGui.Combo("##Filters", ref selected, vFilters, vFilters.Length);
                ImGui.PopItemWidth();
                if (comboClick == true)
                {
                    m_CurrentFilterExt = vFilters[selected];
                }
            }

            ImGui.Spacing();

            ImGui.SameLine(ImGui.GetWindowWidth() - cancelButtonWidth - 20);
            
            if (ImGui.Button("Cancel"))
            {
                IsOk = false;
                res = true;
            }
            cancelButtonWidth = ImGui.GetItemRectSize().X;

            ImGui.SameLine(ImGui.GetWindowWidth() - cancelButtonWidth - okButtonWidth - 30);

            if (ImGui.Button("  Open  "))
            {
                IsOk = true;
                res = true;
            }
            okButtonWidth = ImGui.GetItemRectSize().X;

            ImGui.End();

            if (res == true)
            {
                m_FileList.Clear();
            }

            return res;
        }

        private float cancelButtonWidth = 0, okButtonWidth = 0;
    }
}
