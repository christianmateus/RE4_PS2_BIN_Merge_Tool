using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

namespace RE4_BIN_Util
{
    public partial class Form1 : Form
    {
        OpenFileDialog dialog = new OpenFileDialog();
        string filepath = "";
        public Form1()
        {
            InitializeComponent();
        }

        private void openBINFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            dialog.Filter = "RE4 PS2 BIN Files (*.BIN)|*.BIN";
            dialog.ShowDialog();
            filepath = dialog.FileName;
            if (filepath != "")
            {
                lblBinFileName.Text = Path.GetFileName(filepath);
                btnMergeWith.Enabled = true;
                btnSplitModel.Enabled = true;
                ShowTextureTable(filepath);
            }
            else
            {
                return;
            }
        }
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        private void btnSplitModel_Click(object sender, EventArgs e)
        {
            BinaryReader br = new BinaryReader(File.Open(filepath, FileMode.Open));

            // Get material offset
            br.BaseStream.Position = 0x0A;
            byte materialCount = br.ReadByte();

            // Get material offset
            br.BaseStream.Position = 0x0C;
            uint materialsOffset = br.ReadUInt32();

            // Get every material from the file
            br.BaseStream.Position = materialsOffset;
            List<byte[]> materialList = new List<byte[]>();

            for (int material = 0; material < materialCount; material++)
            {
                materialList.Add(br.ReadBytes(0x10));
            }

            // Get every mesh
            List<byte[]> meshes = new List<byte[]>();
            List<byte[]> meshOffsets = new List<byte[]>();

            // Get all mesh offsets
            for (int i = 0; i < materialCount; i++)
            {
                br.BaseStream.Position = materialsOffset + 0x0C + (i * 0x10);
                meshOffsets.Add(br.ReadBytes(0x04));
            }

            // Stores every mesh inside list
            for (int material = 0; material < materialCount; material++)
            {
                br.BaseStream.Position = BitConverter.ToUInt32(meshOffsets[material], 0);
                if (material != materialCount - 1)
                {
                    meshes.Add(br.ReadBytes((int)(BitConverter.ToUInt32(meshOffsets[material + 1], 0) - BitConverter.ToUInt32(meshOffsets[material], 0))));
                }
                else
                {
                    meshes.Add(br.ReadBytes((int)(br.BaseStream.Length - (int)BitConverter.ToUInt32(meshOffsets[material], 0))));
                }
            }

            // Writes the files separately
            Directory.CreateDirectory(Path.GetFileNameWithoutExtension(filepath));
            for (int i = 0; i < materialCount; i++)
            {
                BinaryWriter bw = new BinaryWriter(
                    File.Open($"{Path.GetFileNameWithoutExtension(filepath)}/" +
                    $"{Path.GetFileNameWithoutExtension(filepath)}_mesh_{i}.MESH", FileMode.Create));

                bw.Write(meshes[i]);
                bw.Close();
            }
            MessageBox.Show($"Extracted {materialCount} meshes from loaded model.", "Information",
                MessageBoxButtons.OK, MessageBoxIcon.Information);

            br.Close();
        }
        private void btnMergeWith_Click(object sender, EventArgs e)
        {
            BinaryReader br = new BinaryReader(File.Open(filepath, FileMode.Open));

            // Get material count
            br.BaseStream.Position = 0x0A;
            byte materialCount = br.ReadByte();

            // Get material starting offset
            br.BaseStream.Position = 0x0C;
            uint materialsOffset = br.ReadUInt32();

            // Splitting the file
            br.BaseStream.Position = materialsOffset + 0x0C;
            uint firstMeshOffset = br.ReadUInt32();

            br.BaseStream.Position = 0;
            byte[] topPart = br.ReadBytes((int)firstMeshOffset);
            byte[] bottomPart = br.ReadBytes((int)(br.BaseStream.Length - firstMeshOffset));

            br.Close();

            dialog.Filter = "RE4 MESH Files (*.MESH)|*.MESH";
            dialog.Multiselect = true;
            dialog.ShowDialog();

            // ----------------------------------
            // Creates new custom merged Bin file
            string mergedBinFilename = Path.GetFileNameWithoutExtension(filepath);
            BinaryWriter bw = new BinaryWriter(File.Open($"{mergedBinFilename}_merged.bin", FileMode.Create));

            // Writes data from start to end of material list
            bw.Write(topPart);
            uint tempAcumulator = 0;
            for (int i = 0; i < dialog.FileNames.Length; i++)
            {

                bw.Write(new byte[] { 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0xFF });
                bw.Write((uint)(tempAcumulator + topPart.Length + bottomPart.Length));

                BinaryReader mesh = new BinaryReader(File.Open(dialog.FileNames[i], FileMode.Open));
                tempAcumulator += (uint)mesh.BaseStream.Length;

                mesh.Close();
            }

            // Writes data after material list
            bw.Write(bottomPart);
            for (int i = 0; i < dialog.FileNames.Length; i++)
            {
                BinaryReader mesh = new BinaryReader(File.Open(dialog.FileNames[i], FileMode.Open));
                bw.Write(mesh.ReadBytes((int)mesh.BaseStream.Length));
                mesh.Close();
            }
            bw.Close();
            UpdateMaterialsOffsets();

            // Checks if user wants to open new created file
            DialogResult result = MessageBox.Show($"Successfully merged {dialog.FileNames.Length} meshes and a new file has been created.\n\nDo you " +
                $"want to open it?", "Information",
                MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (result == DialogResult.Yes)
            {
                filepath = $"{mergedBinFilename}_merged.bin";
                ShowTextureTable($"{mergedBinFilename}_merged.bin");
                lblBinFileName.Text = filepath;
            }
            else
            {
                return;
            }
        }
        private void UpdateMaterialsOffsets()
        {
            string mergedBinFilename = Path.GetFileNameWithoutExtension(filepath);
            BinaryReader br = new BinaryReader(File.Open($"{mergedBinFilename}_merged.bin", FileMode.Open));

            // Get material count
            br.BaseStream.Position = 0x0A;
            byte materialCount = br.ReadByte();

            // Get material starting offset
            br.BaseStream.Position = 0x0C;
            uint materialsOffset = br.ReadUInt32();

            // Splitting the file
            br.BaseStream.Position = materialsOffset + 0x0C;
            List<uint> pointerList = new List<uint>();

            for (int i = 0; i < materialCount + dialog.FileNames.Length; i++)
            {
                pointerList.Add(br.ReadUInt32());
                br.BaseStream.Position += 0x0C;
            }

            br.Close();

            // WRITING DATA
            BinaryWriter bw = new BinaryWriter(File.Open($"{mergedBinFilename}_merged.bin", FileMode.Open));

            // Updates index
            bw.BaseStream.Position = 0x0A;
            bw.Write((byte)(materialCount + dialog.FileNames.Length));

            // Updates material pointers
            bw.BaseStream.Position = materialsOffset + 0x0C;
            for (int i = 0; i < materialCount + dialog.FileNames.Length; i++)
            {
                bw.Write((uint)(pointerList[i] + (0x10 * dialog.FileNames.Length)));
                bw.BaseStream.Position += 0x0C;
            }
            bw.Close();
        }
        private void ShowTextureTable(string file)
        {
            BinaryReader br = new BinaryReader(File.Open(file, FileMode.Open));

            // Get material count
            br.BaseStream.Position = 0x0A;
            byte materialCount = br.ReadByte();

            // Get material starting offset
            br.BaseStream.Position = 0x0C;
            uint materialsOffset = br.ReadUInt32();

            // Get every texture index
            List<byte> textureIndexList = new List<byte>();
            br.BaseStream.Position = materialsOffset + 0x01;

            for (int i = 0; i < materialCount; i++)
            {
                textureIndexList.Add(br.ReadByte());
                br.BaseStream.Position += 0x0F;
            }
            br.Close();

            // Create table
            mainTable.Columns.Clear();
            mainTable.Columns.Add("index", "Texture Index"); // 0
            for (int i = 0; i < materialCount; i++)
            {
                var index = mainTable.Rows.Add();
                mainTable.Rows[index].Cells[0].Value = textureIndexList[i];
            }
        }

        private void saveFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                BinaryReader br = new BinaryReader(File.Open(filepath, FileMode.Open));

                // Get material count
                br.BaseStream.Position = 0x0A;
                byte materialCount = br.ReadByte();

                // Get material starting offset
                br.BaseStream.Position = 0x0C;
                uint materialsOffset = br.ReadUInt32();

                br.Close();

                BinaryWriter bw = new BinaryWriter(File.Open(filepath, FileMode.Open));
                bw.BaseStream.Position = materialsOffset + 0x01;

                for (int i = 0; i < materialCount; i++)
                {
                    if (Convert.ToInt16(mainTable.Rows[i].Cells[0].Value) > Byte.MaxValue)
                    {
                        MessageBox.Show($"Max byte value allowed is 255, wrong value found: {mainTable.Rows[i].Cells[0].Value}", "Error",
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                        bw.Close();
                        return;
                    }
                    bw.Write(Convert.ToByte(mainTable.Rows[i].Cells[0].Value));
                    bw.BaseStream.Position += 0x0F;
                }
                bw.Close();
                MessageBox.Show($"File saved successfully!", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception)
            {
                MessageBox.Show($"Could not save file, check any incorrect index values.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
        }

        private void helpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("This tool was created for quick splitting and merging .bin model meshes," +
                " example: extracting wesker glasses and merging them to leon face model.\n\n" +
                "Note: the .mesh files generated with this tool cannot be viewed in a 3D editor naturally.\n" +
                "Note 2: This tool is not recommended for begginers, as it's experimental and you must understand a bit about .bin file structure.\n\n" +
                "Steps:\n" +
                "1 - Open a bin file you want to extract the meshes or merge something to it;\n" +
                "2 - Click on Split to extract all meshes from loaded model, or Merge With... to load .mesh files;\n" +
                "3 - After merging, click on YES to load the new generated file with '_merged' suffix;\n" +
                "4 - Update the texture indices accordingly to its .tpl texture order.",
                "Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
