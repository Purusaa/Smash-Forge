﻿using System;
using System.Drawing;
using System.Windows.Forms;

namespace SmashForge
{
    public partial class MtaEditorGui : EditorBase
    {
        public MtaEditorGui(MTA mta)
        {
            InitializeComponent();
            this.mta = mta;
        }

        public override void Save()
        {
            if (FilePath.Equals(""))
            {
                SaveAs();
                return;
            }
            FileOutput o = new FileOutput();
            byte[] n = mta.Rebuild();
            o.WriteBytes(n);
            o.Save(FilePath);
            Edited = false;
        }

        public override void SaveAs()
        {
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "Material Animation (.mta)|*.mta|" +
                             "All Files (*.*)|*.*";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    if (sfd.FileName.EndsWith(".mta"))
                    {
                        FilePath = sfd.FileName;
                        Save();
                    }
                }
            }
        }

        private void MTAEditorGUI_Load(object sender, EventArgs e)
        {
            treeView1.Nodes.Add(headerNode);
            treeView1.Nodes.Add(matNode);
            treeView1.Nodes.Add(visNode);
            
            foreach(MatEntry m in mta.matEntries)
            {
                TreeNode mNode = new TreeNode(m.name) { Tag = m };
                matNode.Nodes.Add(mNode);
                foreach(MatData md in m.properties)
                    mNode.Nodes.Add(new TreeNode(md.name) { Tag = md });
            }
            foreach (VisEntry v in mta.visEntries)
                visNode.Nodes.Add(new TreeNode(v.name) { Tag = v });

            treeView1.ExpandAll();
        }

        readonly Font consolas = new Font("Consolas", 11);
        private MTA mta;
        private TreeNode matNode = new TreeNode("Materials");
        private TreeNode visNode = new TreeNode("Visibility");
        private TreeNode headerNode = new TreeNode("Header");

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            colorList1.Visible = false;
            matPropList1.Visible = false;
            if (e.Node == null)
                return;
            if (e.Node == headerNode)
            {
                //If they selected the header node
                
            }
            if (e.Node.Parent == null)
                return;
            if (e.Node.Parent == matNode)
            {
                //If they selected a material header

            }
            if (e.Node.Parent.Parent == null)
                return;
            if (e.Node.Parent.Parent == matNode)
            {
                //If they selected a material property
                MatData m = (MatData)e.Node.Tag;
                if(m.name == "effColorGain" || m.name == "colorGain" || m.name == "aoColorGain" || m.name == "finalColorGain")
                {
                    colorList1.Visible = true;
                    colorList1.Fill(m);
                }
                else
                {
                    matPropList1.Visible = true;
                    matPropList1.Fill(m);
                }
            }
        }

        private void treeView1_AfterLabelEdit(object sender, NodeLabelEditEventArgs e)
        {
            if (e.Node == null || e.Node.Parent == null)
                return;
            if (e.Node.Parent.Parent == matNode)
            {
                //If they selected a material property
                MatData m = (MatData)e.Node.Tag;
                Console.WriteLine(m.name);
                m.name = e.Label;
                e.Node.Text = e.Label;
                Console.WriteLine(m.name);
            }
            treeView1_AfterSelect(treeView1, new TreeViewEventArgs(e.Node));
        }

        private void colorAnimList_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        
    }

    public class ColorKeyframe
    {
        public Color color;
        public int frame;
        public int maxLength;

        public ColorKeyframe(int frame, Color color)
        {
            this.color = color;
            this.frame = frame;
        }

        public override string ToString()
        {
            return $"[{frame.ToString().PadLeft(maxLength)}] R:{color.R.ToString().PadLeft(3)} G:{color.G.ToString().PadLeft(3)} B:{color.B.ToString().PadLeft(3)} A:{color.A.ToString().PadLeft(3)}";
        }
    }

    public class StandardKeyframe
    {
        public int frame;
        public MatData.frame data;
        public string type;
        public int maxLength;

        public StandardKeyframe(int frame, MatData.frame data, string type)
        {
            this.frame = frame;
            this.data = data;
            this.type = type;
        }

        public override string ToString()
        {
            if (type.Equals("NU_colorSamplerUV"))
                return $"[{frame.ToString().PadLeft(maxLength)}] X:{data.values[2].ToString("#0.000").PadLeft(6)} Y:{data.values[3].ToString("#0.000").PadLeft(6)} W:{data.values[0].ToString("#0.000").PadLeft(6)} H:{data.values[1].ToString("#0.000").PadLeft(6)}";

            string r = $"[{frame.ToString().PadLeft(maxLength)}]";
            int i = 64;
            foreach (float value in data.values)
                r += $" {(char)++i}:{value.ToString("#0.000").PadLeft(6)}";
            return r;
        }
    }
}
