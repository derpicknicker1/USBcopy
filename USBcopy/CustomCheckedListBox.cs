using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace USBcopy
{
    public class CustomCheckedListBox : CheckedListBox
    {
        public CustomCheckedListBox()
        {
            DoubleBuffered = false;
        }


        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            try
            {
                Size checkSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, System.Windows.Forms.VisualStyles.CheckBoxState.MixedNormal);
                int dx = (e.Bounds.Height - checkSize.Width) / 2;
                e.DrawBackground();
                if (Items[e.Index].ToString().Contains("Done"))
                {
                    Graphics g = e.Graphics;
                    g.FillRectangle(new SolidBrush(Color.Green), e.Bounds.X + 50 + checkSize.Width, e.Bounds.Y + 1, 100 - checkSize.Width, e.Bounds.Height - 2);
                }
                else if (Items[e.Index].ToString().Contains("Format"))
                {
                    Graphics g = e.Graphics;
                    g.FillRectangle(new SolidBrush(Color.Red), e.Bounds.X + 50 + checkSize.Width, e.Bounds.Y + 1, 100 - checkSize.Width, e.Bounds.Height - 2);
                }
                else if (Items[e.Index].ToString().Contains("Copy"))
                {
                    Graphics g = e.Graphics;
                    g.FillRectangle(new SolidBrush(Color.Yellow), e.Bounds.X + 50 + checkSize.Width, e.Bounds.Y + 1, 100 - checkSize.Width, e.Bounds.Height - 2);
                }
                else
                {
                    Graphics g = e.Graphics;
                    g.FillRectangle(new SolidBrush(BackColor), e.Bounds);
                }

                bool isChecked = GetItemChecked(e.Index);//For some reason e.State doesn't work so we have to do this instead.
                CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(dx, e.Bounds.Top + dx), isChecked ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal);
                using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                {
                    using (Brush brush = new SolidBrush(ForeColor))
                    {
                        float[] tabs = { 50, 130 };
                        sf.SetTabStops(0, tabs);
                        e.Graphics.DrawString(Items[e.Index].ToString(), Font, brush, new Rectangle(e.Bounds.Height, e.Bounds.Top, e.Bounds.Width - e.Bounds.Height, e.Bounds.Height), sf);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
        Color checkedItemColor = Color.Green;
        public Color CheckedItemColor
        {
            get { return checkedItemColor; }
            set
            {
                checkedItemColor = value;
                Invalidate();
            }
        }
    }
}
