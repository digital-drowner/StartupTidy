using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace StartupTidy
{
    internal static class Theme
    {
        internal static readonly Color Background = Color.FromArgb(18, 29, 39);
        internal static readonly Color Surface = Color.FromArgb(25, 39, 52);
        internal static readonly Color Mint = Color.FromArgb(94, 226, 188);
        internal static readonly Color Text = Color.FromArgb(229, 240, 244);
        internal static readonly Color Muted = Color.FromArgb(169, 187, 198);
        internal static readonly Color Border = Color.FromArgb(54, 77, 93);
        internal static readonly Color Selected = Color.FromArgb(37, 80, 70);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
        internal static void TitleBar(Form form,bool dark)
        {
            if (!form.IsHandleCreated) return;
            try { int value=dark?1:0; if(DwmSetWindowAttribute(form.Handle,20,ref value,4)!=0) DwmSetWindowAttribute(form.Handle,19,ref value,4); } catch (DllNotFoundException) {} catch (EntryPointNotFoundException) {}
        }
        internal static void Apply(Control control,bool dark)
        {
            bool surface = control is TabPage || control is ListView || control is TextBoxBase || control is NumericUpDown || control is ComboBox;
            control.BackColor=dark?(surface?Surface:Background):(control is Form?Color.FromArgb(246,248,251):surface?Color.White:Color.FromArgb(246,248,251));
            control.ForeColor=dark?Text:Color.FromArgb(32,40,50);
            var label=control as Label;
            if(label!=null && Object.Equals(label.Tag,"warning")) label.ForeColor=dark?Color.FromArgb(255,196,132):Color.FromArgb(143,76,23);
            var button=control as Button;
            if(button!=null)
            {
                button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderSize=1;
                button.FlatAppearance.BorderColor=dark?Border:Color.FromArgb(184,195,205);
                button.FlatAppearance.MouseOverBackColor=dark?Selected:Color.FromArgb(224,242,235);
                button.FlatAppearance.MouseDownBackColor=dark?Color.FromArgb(45,100,85):Color.FromArgb(199,231,218);
                button.UseVisualStyleBackColor=false;button.BackColor=dark?Surface:Color.White;button.ForeColor=dark?Mint:Color.FromArgb(25,77,61);
            }
            var checkbox=control as CheckBox;
            if(checkbox!=null){checkbox.FlatStyle=dark?FlatStyle.Flat:FlatStyle.Standard;checkbox.UseVisualStyleBackColor=false;}
            var combo=control as ThemedComboBox;if(combo!=null){combo.Dark=dark;combo.FlatStyle=dark?FlatStyle.Flat:FlatStyle.Standard;}
            var numeric=control as NumericUpDown;
            if(numeric!=null) numeric.BorderStyle=BorderStyle.FixedSingle;
            var list=control as ThemedListView;
            if(list!=null)
            {
                list.Dark=dark; list.BorderStyle=dark?BorderStyle.None:BorderStyle.FixedSingle;
                foreach(ListViewItem item in list.Items)
                { var rule=item.Tag as Rule;item.ForeColor=rule!=null && Safety.RuleImportance(rule)!=null ? (dark?Color.FromArgb(255,196,132):Color.FromArgb(143,76,23)) : control.ForeColor; }
            }
            var tabs=control as ThemedTabs;if(tabs!=null)tabs.Dark=dark;
            foreach(Control child in control.Controls)Apply(child,dark);
            var form=control as Form;if(form!=null)TitleBar(form,dark);
            control.Invalidate(true);
        }
        internal static void ApplyMenu(ContextMenuStrip menu,bool dark)
        { if(menu==null)return; menu.Renderer=dark?new ToolStripProfessionalRenderer(new MintMenuColors()):new ToolStripProfessionalRenderer();menu.BackColor=dark?Surface:SystemColors.Control;menu.ForeColor=dark?Text:SystemColors.ControlText;foreach(ToolStripItem item in menu.Items)item.ForeColor=menu.ForeColor; }
        sealed class MintMenuColors:ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground{get{return Surface;}}
            public override Color ImageMarginGradientBegin{get{return Surface;}}
            public override Color ImageMarginGradientMiddle{get{return Surface;}}
            public override Color ImageMarginGradientEnd{get{return Surface;}}
            public override Color MenuItemSelected{get{return Selected;}}
            public override Color MenuItemBorder{get{return Mint;}}
            public override Color MenuBorder{get{return Border;}}
            public override Color SeparatorDark{get{return Border;}}
        }
    }
    internal class ThemedComboBox:ComboBox
    {
        internal bool Dark;
        public ThemedComboBox()
        {
            DrawMode=DrawMode.OwnerDrawFixed;
            DrawItem+=delegate(object sender,DrawItemEventArgs e)
            {
                bool selected=(e.State & DrawItemState.Selected)!=0;
                using(var brush=new SolidBrush(Dark?(selected?Theme.Selected:Theme.Surface):(selected?SystemColors.Highlight:Color.White)))e.Graphics.FillRectangle(brush,e.Bounds);
                string text=e.Index>=0?GetItemText(Items[e.Index]):Text;
                TextRenderer.DrawText(e.Graphics,text,Font,e.Bounds,Dark?Theme.Text:selected?SystemColors.HighlightText:SystemColors.ControlText,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            };
        }
    }
    internal class ThemedTabs:TabControl
    {
        internal bool Dark;
        public ThemedTabs(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);}
        protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Dark ? Theme.Background : Color.FromArgb(246,248,251)); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x318 && m.WParam != IntPtr.Zero)
            { using (var graphics = Graphics.FromHdc(m.WParam)) OnPaint(new PaintEventArgs(graphics, ClientRectangle)); m.Result = IntPtr.Zero; return; }
            base.WndProc(ref m);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Dark ? Theme.Background : Color.FromArgb(246,248,251));
            for(int i=0;i<TabCount;i++)
            {
                Rectangle rect=GetTabRect(i);bool selected=i==SelectedIndex;
                using(var brush=new SolidBrush(Dark?(selected?Theme.Surface:Theme.Background):(selected?Color.White:BackColor)))e.Graphics.FillRectangle(brush,rect);
                TextRenderer.DrawText(e.Graphics,TabPages[i].Text,Font,rect,Dark?(selected?Theme.Mint:Theme.Muted):ForeColor,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                if(selected)using(var pen=new Pen(Dark?Theme.Mint:Color.FromArgb(32,125,95),2))e.Graphics.DrawLine(pen,rect.Left+8,rect.Bottom-2,rect.Right-8,rect.Bottom-2);
            }
        }
        protected override void OnSelectedIndexChanged(EventArgs e){base.OnSelectedIndexChanged(e);Invalidate();}
    }
    internal class ThemedListView:ListView
    {
        internal bool Dark;
        [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
        [StructLayout(LayoutKind.Sequential)] struct ScrollBarInfo
        {
            public int Size; public NativeRect Rect; public int ArrowSize,ThumbTop,ThumbBottom,Reserved;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst=6)] public int[] State;
        }
        [DllImport("user32.dll")] static extern bool GetScrollBarInfo(IntPtr window,int objectId,ref ScrollBarInfo info);
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
        [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr window);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr window,IntPtr dc);
        bool paintingScrollbars;
        bool ReadScrollbar(bool vertical,out ScrollBarInfo info,out Rectangle bounds)
        {
            info=new ScrollBarInfo {Size=Marshal.SizeOf(typeof(ScrollBarInfo)),State=new int[6]}; bounds=Rectangle.Empty;
            if(!IsHandleCreated || !GetScrollBarInfo(Handle,vertical?-5:-6,ref info) || (info.State[0] & 0x8000)!=0) return false;
            NativeRect window; if(!GetWindowRect(Handle,out window))return false;
            bounds=Rectangle.FromLTRB(info.Rect.Left-window.Left,info.Rect.Top-window.Top,info.Rect.Right-window.Left,info.Rect.Bottom-window.Top);
            return bounds.Width>0 && bounds.Height>0;
        }
        internal Rectangle ScrollbarBounds(bool vertical)
        { ScrollBarInfo info;Rectangle bounds;return ReadScrollbar(vertical,out info,out bounds)?bounds:Rectangle.Empty; }
        internal void PaintScrollbars(IntPtr targetDc)
        {
            if(!Dark || !IsHandleCreated || paintingScrollbars)return;
            paintingScrollbars=true;bool own=targetDc==IntPtr.Zero;
            IntPtr dc=own?GetWindowDC(Handle):targetDc;
            try
            {
                if(dc==IntPtr.Zero)return;
                using(var g=Graphics.FromHdc(dc))
                {
                    Rectangle verticalBounds=Rectangle.Empty,horizontalBounds=Rectangle.Empty;
                    for(int axis=0;axis<2;axis++)
                    {
                        bool vertical=axis==0;ScrollBarInfo info;Rectangle bar;
                        if(!ReadScrollbar(vertical,out info,out bar))continue;
                        if(vertical)verticalBounds=bar;else horizontalBounds=bar;
                        using(var brush=new SolidBrush(Theme.Background))g.FillRectangle(brush,bar);
                        Rectangle thumb=vertical?new Rectangle(bar.Left+3,bar.Top+info.ThumbTop,Math.Max(1,bar.Width-6),info.ThumbBottom-info.ThumbTop):
                            new Rectangle(bar.Left+info.ThumbTop,bar.Top+3,info.ThumbBottom-info.ThumbTop,Math.Max(1,bar.Height-6));
                        if(thumb.Width>0 && thumb.Height>0)using(var brush=new SolidBrush(Theme.Muted))g.FillRectangle(brush,thumb);
                        int arrow=Math.Min(info.ArrowSize,vertical?bar.Height/2:bar.Width/2);
                        using(var pen=new Pen(Theme.Mint,1.5f))
                        {
                            for(int end=0;end<2;end++)
                            {
                                int x=vertical?bar.Left+bar.Width/2:(end==0?bar.Left+arrow/2:bar.Right-arrow/2);
                                int y=vertical?(end==0?bar.Top+arrow/2:bar.Bottom-arrow/2):bar.Top+bar.Height/2;
                                int direction=end==0?-1:1;
                                Point[] points=vertical?new[]{new Point(x-3,y-direction),new Point(x,y+direction*2),new Point(x+3,y-direction)}:
                                    new[]{new Point(x-direction,y-3),new Point(x+direction*2,y),new Point(x-direction,y+3)};
                                g.DrawLines(pen,points);
                            }
                        }
                    }
                    if(!verticalBounds.IsEmpty && !horizontalBounds.IsEmpty)
                    using(var brush=new SolidBrush(Theme.Background))g.FillRectangle(brush,new Rectangle(verticalBounds.Left,horizontalBounds.Top,verticalBounds.Width,horizontalBounds.Height));
                }
            }
            finally {if(own && dc!=IntPtr.Zero)ReleaseDC(Handle,dc);paintingScrollbars=false;}
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            // Keep native scrolling and hit testing; repaint only the non-client scrollbar chrome.
            switch(m.Msg)
            {
                case 0xF: case 0x85: case 0xA0: case 0xA1: case 0xA2: case 0xA3:
                case 0x114: case 0x115: case 0x20A: case 0x20E: case 0x5: case 0x31A:
                    PaintScrollbars(IntPtr.Zero);break;
                case 0x317: PaintScrollbars(m.WParam);break;
            }
        }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if(Columns.Count>0)
            {
                int used=0;for(int i=0;i<Columns.Count-1;i++)used+=Columns[i].Width;
                int width=Math.Max(220,ClientSize.Width-used);
                if(Columns[Columns.Count-1].Width!=width)Columns[Columns.Count-1].Width=width;
            }
        }
        public ThemedListView()
        {
            DoubleBuffered=true;OwnerDraw=true;
            DrawColumnHeader+=delegate(object sender,DrawListViewColumnHeaderEventArgs e)
            {
                if(!Dark){e.DrawDefault=true;return;}
                using(var brush=new SolidBrush(Theme.Background))e.Graphics.FillRectangle(brush,e.Bounds);
                using(var pen=new Pen(Theme.Border))e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1);
                var bounds=e.Bounds;bounds.Inflate(-7,0);TextRenderer.DrawText(e.Graphics,e.Header.Text,Font,bounds,Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            };
            DrawItem+=delegate(object sender,DrawListViewItemEventArgs e){if(!Dark)e.DrawDefault=true;};
            DrawSubItem+=delegate(object sender,DrawListViewSubItemEventArgs e)
            {
                if(!Dark){e.DrawDefault=true;return;}
                using(var brush=new SolidBrush(e.Item.Selected?Theme.Selected:Theme.Surface))e.Graphics.FillRectangle(brush,e.Bounds);
                Rectangle text=e.Bounds;text.X+=7;text.Width-=10;
                if(e.ColumnIndex==0 && CheckBoxes)
                {
                    var box=new Rectangle(e.Bounds.Left+6,e.Bounds.Top+(e.Bounds.Height-13)/2,13,13);
                    using(var brush=new SolidBrush(e.Item.Checked?Theme.Mint:Theme.Background))e.Graphics.FillRectangle(brush,box);
                    using(var pen=new Pen(e.Item.Checked?Theme.Mint:Theme.Muted))e.Graphics.DrawRectangle(pen,box);
                    if(e.Item.Checked)using(var pen=new Pen(Theme.Background,2))e.Graphics.DrawLines(pen,new[]{new Point(box.Left+3,box.Top+6),new Point(box.Left+5,box.Top+9),new Point(box.Left+10,box.Top+3)});
                    text.X+=18;text.Width-=18;
                }
                TextRenderer.DrawText(e.Graphics,e.SubItem.Text,Font,text,e.Item.ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
                if(e.Item.Focused && Focused && e.ColumnIndex==0)ControlPaint.DrawFocusRectangle(e.Graphics,e.Bounds,Theme.Mint,Theme.Selected);
            };
        }
    }
}
