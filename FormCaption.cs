using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AdamS2T2Docs
{
    public partial class FormCaption : Form
    {
        private bool dragging = false;
        private Point dragCursorPoint;
        private Point dragFormPoint;

        private int resizeBorderWidth = 8; // Adjust this value for your desired resizing sensitivity

        public FormCaption()
        {
            InitializeComponent();

            // Subscribe to the event handlers here
            this.MouseDown += FormCaption_MouseDown;
            this.MouseMove += FormCaption_MouseMove;
            this.MouseUp += FormCaption_MouseUp;

            // Add resizing handles
            this.ResizeRedraw = true;
        }

        private void FormCaption_Load(object sender, EventArgs e)
        {
            // Set the form's opacity and transparency key
            this.Opacity = 0.8;
            this.TransparencyKey = Color.Transparent;
        }

        // Event handler for mouse down event to enable dragging
        private void FormCaption_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                dragging = true;
                dragCursorPoint = Cursor.Position;
                dragFormPoint = this.Location;
            }
        }

        // Event handler for mouse move event to handle dragging
        private void FormCaption_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Point dif = Point.Subtract(Cursor.Position, new Size(dragCursorPoint));
                this.Location = Point.Add(dragFormPoint, new Size(dif));
            }
        }

        // Event handler for mouse up event to stop dragging
        private void FormCaption_MouseUp(object sender, MouseEventArgs e)
        {
            dragging = false;
        }




        // Event handler for resizing the form
        private void FormCaption_Resize(object sender, EventArgs e)
        {
            // Add your resizing logic here if needed
        }

        // Event handler for resizing the form
        protected override void WndProc(ref Message m)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTBOTTOMRIGHT = 17;

            base.WndProc(ref m);

            if (m.Msg == WM_NCHITTEST)
            {
                int x = (int)(m.LParam.ToInt64() & 0xFFFF);
                int y = (int)((m.LParam.ToInt64() & 0xFFFF0000) >> 16);
                Point pt = PointToClient(new Point(x, y));

                if (pt.X >= ClientSize.Width - resizeBorderWidth && pt.Y >= ClientSize.Height - resizeBorderWidth)
                {
                    m.Result = (IntPtr)HTBOTTOMRIGHT;
                }
            }
        }
    }
}
