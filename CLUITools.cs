using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PSCLUITools
{
    class Buffer : PSCmdlet
    {
        protected static int left = Console.WindowLeft;
        internal static int top = Console.WindowTop;
        internal static Coordinates position = new Coordinates(left, top);
        internal static int width = Console.WindowWidth;
        internal static int height = Console.WindowHeight;
        protected Container container = new Container(left, top, width, height);
        internal PSHost PSHost { get; set;}

        protected static char[,] screenBufferArray = new char[width,height];
        internal List<BufferCellElement> bufferCellElements = new List<BufferCellElement>();
        protected List<Control> BufferedControls = new List<Control>();

        public Buffer()
        {
            this.container.SetContainerToWidestControlWidth = false;
            this.container.SetControlsToContainerWidth = false;
            this.container.AutoPositionControls = false;
        }

        public Buffer(PSHost host)
        {
            this.PSHost = host;
            this.container.SetContainerToWidestControlWidth = false;
            this.container.SetControlsToContainerWidth = false;
            this.container.AutoPositionControls = false;
        }

        protected void Insert(int column, int row, List<string> text)
        {
            if (this.PSHost == null)
            {
                foreach (string txt in text)
                {
                    var txtArr = (Char[]) txt.ToCharArray(0,txt.Length);
                    var i = 0;
                    foreach (char c in txtArr)
                    {
                        screenBufferArray[column + i, row] = c;
                        i++;
                    }
                    row++;
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        public void Update(Control control)
        {
            if (this.PSHost == null)
            {
                if (control is Container)
                {
                    var container = (Container) control;
                    foreach (Control childControl in container.controls)
                        this.Update(childControl);
                }
                else
                {
                    var left = control.Position.X;
                    var top = control.Position.Y;
                    var text = control.GetTextRepresentation();
                    this.Insert(left, top, text);
                }
            }
            else
            {
                if (control is Container)
                {
                    var container = (Container) control;
                    foreach (Control childControl in container.controls)
                        this.Update(childControl);
                }
                else
                {
                    if (!this.BufferedControls.Contains(control))
                    {
                        foreach (BufferCellElement bce in control.GetPSHostRawUIRepresentation())
                            this.bufferCellElements.Add(bce);
                        this.BufferedControls.Add(control);
                    }
                }
            }
        }

        public void UpdateAll()
        {
            foreach (Control control in this.container.controls)
            {
                this.Update(control);
            }
        }

        public void Write()
        {
            if (this.PSHost == null)
            {
                var screenBuffer = "";
                // Iterate through buffer, adding each value to screenBuffer
                for (int row = 0; row < height - 1; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        var character = screenBufferArray[column, row];
                        if ((char) character == 0)
                        {
                            // On Windows Terminal empty indices in the char array 
                            // default to no character instead of a space so add one 
                            // of them instead
                            character = ' ';
                        }
                        screenBuffer += character;
                    }
                }
                // Set cursor position to top left and draw the string
                Console.SetCursorPosition(left, top);
                Console.Write(screenBuffer);
                screenBufferArray = new char[width, height];
            }
            else
            {
                foreach (BufferCellElement bce in this.bufferCellElements)
                {
                    // Only write changed Buffer Cell Elements
                    if (bce.Changed)
                    {
                        bce.Changed = false;
                        this.PSHost.UI.RawUI.SetBufferContents(bce.Coordinates, bce.NewBufferCellArray);
                    }
                }
            }
        }
        
        internal void Clear()
        {
            if (this.PSHost != null)
            {
                foreach (BufferCellElement bce in this.bufferCellElements)
                    this.PSHost.UI.RawUI.SetBufferContents(bce.Coordinates, bce.CapturedBufferCellArray);
                this.BufferedControls = new List<Control>();
                this.bufferCellElements = new List<BufferCellElement>();
            }
        }

        public void Add(Control control)
        {
            control.Buffer = this;
            this.container.controls.Add(control);
        }

        public void Remove(Control control)
        {
            if (this.PSHost != null)
            {
                foreach (BufferCellElement bce in this.bufferCellElements)
                {
                    if (bce.Control == control)
                        this.PSHost.UI.RawUI.SetBufferContents(bce.Coordinates, bce.CapturedBufferCellArray);
                }
            }
            control.Buffer = this;
            this.container.controls.Remove(control);
        }

        public void RemoveFromBuffer(Control control)
        {
            // TODO Restore buffer
            for (int i = 0; i < this.bufferCellElements.Count; i++)
            {
                BufferCellElement bce = this.bufferCellElements[i];
                if (bce.Control == control)
                    bufferCellElements.Remove(bce);
            }

            this.BufferedControls.Remove(control);
        }

        public BufferCellElement GetBufferCellElement(Object searchObject)
        {
            foreach (BufferCellElement bce in bufferCellElements)
            {
                if (bce.Item == searchObject)
                    return bce;
            }
            return null;
        }
    }

    class BufferCellElement
    {
        internal BufferCell[,] CapturedBufferCellArray { get; set; }
        internal BufferCell[,] NewBufferCellArray { get; set; }
        internal Coordinates Coordinates { get; set;}
        internal Control Control { get; set; }
        internal Object Item { get; set; }
        internal bool Changed { get; set; } = true;
        
        public BufferCellElement(BufferCell[,] capturedBufferCellArray, 
            BufferCell[,] newBufferCellArray, Coordinates coordinates)
        {
            this.CapturedBufferCellArray = capturedBufferCellArray;
            this.NewBufferCellArray = newBufferCellArray;
            this.Coordinates = coordinates;
        }

        public BufferCellElement(BufferCell[,] capturedBufferCellArray, 
            BufferCell[,] newBufferCellArray, Coordinates coordinates, Control control)
        {
            this.CapturedBufferCellArray = capturedBufferCellArray;
            this.NewBufferCellArray = newBufferCellArray;
            this.Coordinates = coordinates;
            this.Control = control;
        }

        public BufferCellElement(BufferCell[,] capturedBufferCellArray, 
            BufferCell[,] newBufferCellArray, Coordinates coordinates, Object obj)
        {
            this.CapturedBufferCellArray = capturedBufferCellArray;
            this.NewBufferCellArray = newBufferCellArray;
            this.Coordinates = coordinates;
            this.Item = obj;
        }

        public BufferCellElement(BufferCell[,] capturedBufferCellArray, 
            BufferCell[,] newBufferCellArray, Coordinates coordinates, Control control, Object obj)
        {
            this.CapturedBufferCellArray = capturedBufferCellArray;
            this.NewBufferCellArray = newBufferCellArray;
            this.Coordinates = coordinates;
            this.Control = control;
            this.Item = obj;
        }
    }

    abstract class Control : PSCmdlet
    {
        // TODO Change accessibility (public, protected, etc) to whatever it ought to be
        public Coordinates Position { get; set; } = new Coordinates(0, 0);
        protected int width = 0;
        protected int height = 0;
        protected bool BorderTop { get; set; } = false;
        protected bool BorderRight { get; set; } = false;
        protected bool BorderBottom { get; set; } = false;
        protected bool BorderLeft { get; set; } = false;
        protected bool PaddingTop { get; set; } = false;
        protected bool PaddingRight { get; set; } = false;
        protected bool PaddingBottom { get; set; } = false;
        protected bool PaddingLeft { get; set; } = false;
        public char BorderCharacter { get; set; } = '#';
        public BufferCell BorderCell { get; set; } = new BufferCell(' ', 0, 0, 0);
        public char PaddingCharacterTop { get; set; } = ' ';
        public char PaddingCharacterRight { get; set; } = ' ';
        public char PaddingCharacterBottom { get; set; } = ' ';
        public char PaddingCharacterLeft { get; set; } = ' ';
        public BufferCell PaddingCellTop { get; set; } = new BufferCell(' ', 0, 0, 0);
        public BufferCell PaddingCellRight { get; set; } = new BufferCell(' ', 0, 0, 0);
        public BufferCell PaddingCellBottom { get; set; } = new BufferCell(' ', 0, 0, 0);
        public BufferCell PaddingCellLeft { get; set; } = new BufferCell(' ', 0, 0, 0);
        public char FillCharacter { get; set; } = ' ';
        public BufferCell FillCell { get; set; } = new BufferCell(' ', 0, 0, 0);
        public char SelectCharacter { get; set; } = '+';
        public char ActiveCharacter { get; set; } = '>';
        public char SelectedAndActiveCharacter { get; set; } = '*';
        public ConsoleColor BackgroundColor { get; set; }
        public ConsoleColor ForegroundColor { get; set; }
        public ConsoleColor ActiveItemForegroundColor { get; set; }
        public ConsoleColor SelectedItemForegroundColor { get; set; }
        public ConsoleColor ActiveAndSelectedItemForegroundColor { get; set; }
        public string AlignText { get; set; } = "Left";

        // Controls
        public const string KeyUp0  = "UpArrow";
        public const string KeyUp1  = "K";
        public const string KeyRight0  = "RightArrow";
        public const string KeyRight1  = "L";
        public const string KeyDown0  = "DownArrow";
        public const string KeyDown1  = "J";
        public const string KeyPageUp  = "PageUp";
        public const string KeyPageDown  = "PageDown";
        public const string KeyLeft0  = "LeftArrow";
        public const string KeyLeft1  = "H";
        public const string KeyConfirm = "Enter";
        public const string KeySelect = "Spacebar";
        public const string KeyCancel = "Escape";
        public const string KeyFind = "Oem2";
        public const string KeyFindNext = "N";
        public const string KeyFindPrevious = "P";
        public const string KeyTest = "T";

        // Returns a text representation of the control, including borders and whatever else stylings
        public abstract List<string> GetTextRepresentation();

        // Returns a PSHost representation of the control
        public abstract List<BufferCellElement> GetPSHostRawUIRepresentation();

        // A Container that contains this Control
        public Container Container { get; set; }

        public Buffer Buffer { get; set; }
        // TODO Respect boundaries of other Controls within the Container
        // NOTE Should probably just scope for Container to arrange Controls within it either vertically or 
        //      horizontally for now, maybe even remove any direct way of sizing and positioning of other 
        //      types of Control

        public void UpdatePSHostVariables()
        {
            this.BackgroundColor = this.Buffer.PSHost.UI.RawUI.ForegroundColor;
            this.ForegroundColor = this.Buffer.PSHost.UI.RawUI.BackgroundColor;
            this.ActiveItemForegroundColor = ConsoleColor.Green;
            this.SelectedItemForegroundColor = ConsoleColor.Magenta;
            this.ActiveAndSelectedItemForegroundColor = ConsoleColor.Cyan;
            this.BorderCell = new BufferCell(this.BorderCharacter, 
                this.ForegroundColor, this.BackgroundColor, 0);
            this.PaddingCellTop = new BufferCell(this.PaddingCharacterTop, 
                this.ForegroundColor, this.BackgroundColor, 0);
            this.PaddingCellRight = new BufferCell(this.PaddingCharacterRight, 
                this.ForegroundColor, this.BackgroundColor, 0);
            this.PaddingCellBottom = new BufferCell(this.PaddingCharacterBottom, 
                this.ForegroundColor, this.BackgroundColor, 0);
            this.PaddingCellLeft = new BufferCell(this.PaddingCharacterLeft, 
                this.ForegroundColor, this.BackgroundColor, 0);
            this.FillCell = new BufferCell(this.FillCharacter, 
                this.ForegroundColor, this.BackgroundColor, 0);
        }
        
        public void SetHorizontalPosition(int x)
        {
            if (this.Container != null)
            {
                if (x + this.GetWidth() > this.Container.GetRightEdgePosition())
                {
                    this.Position = new Coordinates(x, this.Position.Y);
                    this.SetRightEdgePosition(this.Container.GetRightEdgePosition());
                    return;
                }

                if (x < this.Container.GetLeftEdgePosition())
                {
                    this.SetLeftEdgePosition(this.Container.GetLeftEdgePosition());
                    // Remove the number of characters passing left side of the Container
                    width = this.GetWidth();
                    width = width - (this.Container.GetLeftEdgePosition() - x);
                    this.SetWidth(width);
                    return;
                }
            }
            
            this.Position = new Coordinates(x, this.Position.Y);
        }

        public void SetVerticalPosition(int y)
        {
            if (this.Container != null)
            {
                if (y + this.GetHeight() > this.Container.GetBottomEdgePosition())
                {
                    this.Position = new Coordinates(this.Position.X, y);
                    this.SetBottomEdgePosition(this.Container.GetBottomEdgePosition());
                    return;
                }

                if (y < this.Container.GetTopEdgePosition())
                {
                    this.SetTopEdgePosition(this.Container.GetTopEdgePosition());
                    // Remove the number of characters passing top side of the Container
                    height = this.GetHeight();
                    height = height - (this.Container.GetTopEdgePosition() - y);
                    this.SetHeight(height);
                    return;
                }
            }

            this.Position = new Coordinates(this.Position.X, y);
        }

        public void SetWidth(int width)
        {
            if (this.Container != null)
            {
                if (this.Container.SetContainerToWidestControlWidth && width > this.Container.GetWidth())
                {
                    this.Container.SetWidth(width);
                    this.width = width;
                }
                else if (width > this.Container.GetWidth())
                    width = this.Container.GetWidth();
                else if (this.GetLeftEdgePosition() > this.Container.GetRightEdgePosition())
                    // Left edge tried to pass container right edge
                    width = 0;
                else if (this.GetLeftEdgePosition() + width > this.Container.GetRightEdgePosition())
                {
                    // Right edge tried to pass Container right edge
                    this.SetRightEdgePosition(this.Container.GetRightEdgePosition());
                    return;
                }
                else if (this.GetLeftEdgePosition() + width < this.Container.GetLeftEdgePosition())
                    // Right edge tried to pass Container left edge
                    width = 0;
            }

            // FIX I do not want this here but it works as a workaround for now. The buffer is the only place 
            //     that Console.WindowWidth should be used so it can be just changed to something else there. 
            //     Just check with the width of the buffer, however that's going to end up being done...
            if (width > Console.WindowWidth)
                width = Console.WindowWidth;

            this.width = width;
        }

        public int GetWidth()
        {
            return this.width;
        }

        public void SetHeight(int height)
        {
            if (this.Container != null)
            {
                if (this.Container.SetContainerToWidestControlWidth && height > this.Container.GetHeight())
                {
                    this.Container.SetHeight(height);
                    this.height = height;
                }
                else if (height > this.Container.GetHeight())
                    height = this.Container.GetHeight();
                else if (this.GetTopEdgePosition() > this.Container.GetBottomEdgePosition())
                    // Top edge tried to pass container bottom edge
                    this.height = 0;
                else if (this.GetTopEdgePosition() + height > this.Container.GetBottomEdgePosition())
                {
                    // Bottom edge tried to pass Container bottom edge
                    this.SetBottomEdgePosition(this.Container.GetBottomEdgePosition());
                    return;
                }
                else if (this.GetTopEdgePosition() + height < this.Container.GetTopEdgePosition())
                    // Bottom edge tried to pass container top edge
                    height = 0;
            }

            // FIX Decreasing two prevents the bottom from escaping outside of the window/buffer. Try 
            //     to find and fix the actual problem, this is a workaround.
            if (height >= Console.WindowHeight)
                height = Console.WindowHeight - 2;

            this.height = height;
        }

        public int GetHeight()
        {
            return this.height;
        }

        public void AddBorder(string edge)
        {
            switch (edge)
            {
                case "top":
                    if (!this.BorderTop)
                    {
                        this.SetHeight(this.GetHeight() + 1);
                        this.BorderTop = true;
                        // TODO Should also maybe move position up 1 row?
                    }
                    break;
                case "right":
                    if (!this.BorderRight)
                    {
                        this.SetWidth(this.GetWidth() + 1);
                        this.BorderRight = true;
                    }
                    break;
                case "bottom":
                    if (!this.BorderBottom)
                    {
                        this.SetHeight(this.GetHeight() + 1);
                        this.BorderBottom = true;
                    }
                    break;
                case "left":
                    if (!this.BorderLeft)
                    {
                        this.SetWidth(this.GetWidth() + 1);
                        this.BorderLeft = true;
                        // TODO Should also maybe move position left 1 column?
                    }
                    break;
                case "all":
                    this.AddBorder("top");
                    this.AddBorder("right");
                    this.AddBorder("bottom");
                    this.AddBorder("left");
                    break;
            }
        }

        public void RemoveBorder(string edge)
        {
            switch (edge)
            {
                case "top":
                    if (this.BorderTop)
                    {
                        this.SetHeight(this.GetHeight() - 1);
                        this.BorderTop = false;
                        // TODO Should also maybe move position down 1 row?
                    }
                    break;
                case "right":
                    if (this.BorderRight)
                    {
                        this.SetWidth(this.GetWidth() - 1);
                        this.BorderRight = false;
                    }
                    break;
                case "bottom":
                    if (this.BorderBottom)
                    {
                        this.SetHeight(this.GetHeight() - 1);
                        this.BorderBottom = false;
                    }
                    break;
                case "left":
                    if (this.BorderLeft)
                    {
                        this.SetWidth(this.GetWidth() - 1);
                        this.BorderLeft = false;
                        // TODO Should also maybe move position right 1 column?
                    }
                    break;
                case "all":
                    this.RemoveBorder("top");
                    this.RemoveBorder("right");
                    this.RemoveBorder("bottom");
                    this.RemoveBorder("left");
                    break;
            }
        }

        public void AddPadding(string edge)
        {
            switch (edge)
            {
                case "top":
                    if (!this.PaddingTop)
                    {
                        this.SetHeight(this.GetHeight() + 1);
                        this.PaddingTop = true;
                    }
                    break;
                case "right":
                    if (!this.PaddingRight)
                    {
                        this.SetWidth(this.GetWidth() + 1);
                        this.PaddingRight = true;
                    }
                    break;
                case "bottom":
                    if (!this.PaddingBottom)
                    {
                        this.SetHeight(this.GetHeight() + 1);
                        this.PaddingBottom = true;
                    }
                    break;
                case "left":
                    if (!this.PaddingLeft)
                    {
                        this.SetWidth(this.GetWidth() + 1);
                        this.PaddingLeft = true;
                    }
                    break;
                case "all":
                    this.AddPadding("top");
                    this.AddPadding("right");
                    this.AddPadding("bottom");
                    this.AddPadding("left");
                    break;
            }
        }

        public void RemovePadding(string edge)
        {
            switch (edge)
            {
                case "top":
                    if (this.PaddingTop)
                    {
                        this.SetHeight(this.GetHeight() - 1);
                        this.PaddingTop = false;
                    }
                    break;
                case "right":
                    if (this.PaddingRight)
                    {
                        this.SetWidth(this.GetWidth() - 1);
                        this.PaddingRight = false;
                    }
                    break;
                case "bottom":
                    if (this.PaddingBottom)
                    {
                        this.SetHeight(this.GetHeight() - 1);
                        this.PaddingBottom = false;
                    }
                    break;
                case "left":
                    if (this.PaddingLeft)
                    {
                        this.SetWidth(this.GetWidth() - 1);
                        this.PaddingLeft = false;
                    }
                    break;
                case "all":
                    this.RemovePadding("top");
                    this.RemovePadding("right");
                    this.RemovePadding("bottom");
                    this.RemovePadding("left");
                    break;
            }
        }

        public int GetLeftEdgePosition()
        {
            return this.Position.X;
        }

        public void SetLeftEdgePosition(int x)
        {
            if (this.Container != null)
            {
                if (x < this.Container.GetLeftEdgePosition())
                    x = this.Container.GetLeftEdgePosition();
            }

            this.Position = new Coordinates(x, this.Position.Y);
        }

        public int GetTopEdgePosition()
        {
            return this.Position.Y;
        }

        public void SetTopEdgePosition(int y)
        {
            if (this.Container != null)
            {
                if (y < this.Container.GetTopEdgePosition())
                    y = this.Container.GetTopEdgePosition();
            }

            this.Position = new Coordinates(this.Position.X, y);
        }

        public int GetRightEdgePosition()
        {
            return this.Position.X + this.GetWidth();
        }

        public void SetRightEdgePosition(int x)
        {
            if (this.Container != null)
            {
                if (x > this.Container.GetRightEdgePosition())
                    x = this.Container.GetRightEdgePosition();
            }

            if (x < this.GetLeftEdgePosition())
                this.SetWidth(0);
            else
                this.SetWidth(x - this.GetLeftEdgePosition());
        }

        public int GetBottomEdgePosition()
        {
            return this.Position.Y + this.GetHeight();
        }
        
        public void SetBottomEdgePosition(int y)
        {
            if (this.Container != null)
            {
                if (y > this.Container.GetBottomEdgePosition())
                    y = this.Container.GetBottomEdgePosition();
            }

            if (y < this.GetTopEdgePosition())
                this.SetHeight(0);
            else
                this.SetHeight(y - this.GetTopEdgePosition());
        }

        // Get top border positions
        public int GetTopBorderPositionTop()
        {
            return this.GetTopEdgePosition();
        }

        public int GetTopBorderPositionBottom()
        {
            return this.GetTopBorderPositionTop() + 1;
        }

        public int GetTopBorderPositionLeft()
        {
            return this.GetLeftEdgePosition();
        }

        public int GetTopBorderPositionRight()
        {
            return this.GetRightEdgePosition() - 1;
        }
        
        // Get bottom border positions
        public int GetBottomBorderPositionTop()
        {
            return this.GetBottomEdgePosition() - 1;
        }

        public int GetBottomBorderPositionBottom()
        {
            return this.GetBottomBorderPositionTop() + 1;
        }

        public int GetBottomBorderPositionLeft()
        {
            return this.GetTopBorderPositionLeft();
        }

        public int GetBottomBorderPositionRight()
        {
            return this.GetTopBorderPositionRight();
        }
        
        // Get left border positions
        public int GetLeftBorderPositionTop()
        {
            if (this.BorderTop)
                return this.GetTopBorderPositionBottom();
            return this.GetTopBorderPositionTop();
        }

        public int GetLeftBorderPositionBottom()
        {
            if (this.BorderBottom)
                return this.GetBottomBorderPositionTop() - 1;
            return this.GetBottomBorderPositionBottom() - 1;
        }

        public int GetLeftBorderPositionLeft()
        {
            return this.GetTopBorderPositionLeft();
        }

        public int GetLeftBorderPositionRight()
        {
            return this.GetLeftBorderPositionLeft() + 1;
        }

        public int GetLeftBorderHeight()
        {
            var height = this.GetHeight();
            if (this.BorderTop)
                height = height - 1;
            if (this.BorderBottom)
                height = height - 1;
            return height;
        }

        // Get right border positions
        public int GetRightBorderPositionTop()
        {
            if (this.BorderTop)
                return this.GetTopBorderPositionBottom();
            return this.GetTopBorderPositionTop();
        }

        public int GetRightBorderPositionBottom()
        {
            if (this.BorderBottom)
                return this.GetBottomBorderPositionTop() - 1;
            return this.GetBottomBorderPositionBottom() - 1;
        }

        public int GetRightBorderPositionLeft()
        {
            return this.GetTopBorderPositionRight();
        }

        public int GetRightBorderPositionRight()
        {
            return this.GetRightBorderPositionLeft() + 1;
        }

        public int GetRightBorderHeight()
        {
            return this.GetLeftBorderHeight();
        }

        // Get top padding positions
        public int GetTopPaddingPositionTop()
        {
            if (this.BorderTop)
                return this.GetTopBorderPositionTop() + 1;
            return this.GetTopBorderPositionTop();
        }

        public int GetTopPaddingPositionBottom()
        {
            return this.GetTopPaddingPositionTop() + 1;
        }

        public int GetTopPaddingPositionLeft()
        {
            if (this.BorderLeft)
                return this.GetTopBorderPositionLeft() + 1;
            return this.GetTopBorderPositionLeft();
        }

        public int GetTopPaddingPositionRight()
        {
            if (this.BorderRight)
                return this.GetTopBorderPositionRight() - 1;
            return this.GetTopBorderPositionRight();
        }

        public int GetTopPaddingWidth()
        {
            var width = this.GetWidth();
            if (this.BorderLeft)
                width = width - 1;
            if (this.BorderRight)
                width = width - 1;
            return width;
        }
        
        // Get bottom padding positions
        public int GetBottomPaddingPositionTop()
        {
            if (this.BorderBottom)
                return this.GetBottomBorderPositionTop() - 1;
            return this.GetBottomBorderPositionTop();
        }

        public int GetBottomPaddingPositionBottom()
        {
            return this.GetBottomPaddingPositionTop() + 1;
        }

        public int GetBottomPaddingPositionLeft()
        {
            if (this.BorderLeft)
                return this.GetTopBorderPositionLeft() + 1;
            return this.GetTopBorderPositionLeft();
        }

        public int GetBottomPaddingPositionRight()
        {
            if (this.BorderRight)
                return this.GetBottomBorderPositionRight() - 1;
            return this.GetBottomBorderPositionRight();
        }
        
        public int GetBottomPaddingWidth()
        {
            return this.GetTopPaddingWidth();
        }
        
        // Get left padding positions
        public int GetLeftPaddingPositionTop()
        {
            var position = this.GetTopEdgePosition();
            if (this.PaddingTop)
                position = this.GetTopPaddingPositionBottom();
            else if (this.BorderTop)
                position = this.GetTopBorderPositionBottom();
            return position;

        }

        public int GetLeftPaddingPositionBottom()
        {
            var position = this.GetBottomEdgePosition() - 1;
            if (this.PaddingBottom)
                position = this.GetBottomPaddingPositionTop() - 1;
            if (this.BorderBottom)
                position = this.GetBottomBorderPositionTop() - 1;
            return position;
        }

        public int GetLeftPaddingPositionLeft()
        {
            var position = this.GetLeftEdgePosition();
            if (this.BorderLeft)
                position = position += 1;
            return position;
        }

        public int GetLeftPaddingPositionRight()
        {
            return this.GetLeftPaddingPositionLeft() + 1;
        }

        public int GetLeftPaddingHeight()
        {
            var height = this.GetHeight();
            if (this.BorderTop)
                height = height - 1;
            if (this.BorderBottom)
                height = height - 1;
            if (this.PaddingTop)
                height = height - 1;
            if (this.PaddingBottom)
                height = height - 1;
            return height;
        }

        // Get right padding positions
        public int GetRightPaddingPositionTop()
        {
            var position = this.GetTopEdgePosition();
            if (this.PaddingTop)
                position = this.GetTopPaddingPositionBottom();
            else if (this.BorderTop)
                position = this.GetTopBorderPositionBottom();
            return position;
        }

        public int GetRightPaddingPositionBottom()
        {
            var position = this.GetBottomEdgePosition() - 1;
            if (this.PaddingBottom)
                position = this.GetBottomPaddingPositionTop() - 1;
            if (this.BorderBottom)
                position = this.GetBottomBorderPositionTop() - 1;
            return position;
        }

        public int GetRightPaddingPositionLeft()
        {
            var position = this.GetRightEdgePosition() - 1;
            if (this.BorderRight)
                position = position -= 1;
            return position;
        }

        public int GetRightPaddingPositionRight()
        {
            return this.GetRightPaddingPositionLeft() + 1;
        }

        public int GetRightPaddingHeight()
        {
            return this.GetLeftPaddingHeight();
        }

        // Get item positions
        public int GetItemPositionTop(int itemNumber = 0)
        {
            var position = this.GetTopEdgePosition();
            if (this.PaddingTop)
                position = this.GetTopPaddingPositionBottom();
            else if (this.BorderTop)
                position = this.GetTopBorderPositionBottom();
            position += itemNumber;
            return position;
        }

        public int GetItemPositionBottom(int itemNumber = 0)
        {
            return this.GetItemPositionTop(itemNumber) + 1;
        }

        public int GetItemPositionLeft()
        {
            var position = this.GetLeftEdgePosition();
            if (this.PaddingLeft)
                position = this.GetLeftPaddingPositionRight();
            else if (this.BorderLeft)
                position = this.GetLeftBorderPositionRight();
            return position;
        }

        public int GetItemPositionRight()
        {
            var position = this.GetRightEdgePosition() - 1;
            if (this.PaddingRight)
                position = this.GetRightPaddingPositionLeft() - 1;
            else if (this.PaddingLeft)
                position = this.GetRightPaddingPositionLeft() - 1;
            return position;
        }

        public int GetItemWidth()
        {
            var width = this.GetWidth();
            if (this.BorderLeft)
                width = width - 1;
            if (this.BorderRight)
                width = width - 1;
            if (this.PaddingLeft)
                width = width - 1;
            if (this.PaddingRight)
                width = width - 1;
            return width;
        }

        public int GetItemHorizontalSpace()
        {
            var textHorizontalSpace = this.GetWidth();

            if (this.BorderRight)
                textHorizontalSpace--;

            if (this.BorderLeft)
                textHorizontalSpace--;
            
            if (this.PaddingRight)
                textHorizontalSpace--;

            if (this.PaddingLeft)
                textHorizontalSpace--;

            if (textHorizontalSpace < 0)
                textHorizontalSpace = 0;
            
            return textHorizontalSpace;
        }

        public BufferCellElement NewBufferCellElement(string element, List<string> text = null, 
                                                        int itemNumber = 0, Control control = null, 
                                                        Object obj = null)
        {
            var positionTop = 0; 
            var positionBottom = 0;
            var positionLeft = 0;
            var positionRight = 0;
            var width = 1;
            var height = 1;
            var cell = this.BorderCell;

            if (element == "borderTop")
            {
                positionTop = this.GetTopBorderPositionTop();
                positionBottom = this.GetTopBorderPositionBottom();
                positionLeft = this.GetTopBorderPositionLeft();
                positionRight = this.GetTopBorderPositionRight();
                width = this.GetWidth();
                cell = this.BorderCell;
            }
            else if (element == "borderBottom")
            {
                positionTop = this.GetBottomBorderPositionTop();
                positionBottom = this.GetBottomBorderPositionBottom();
                positionLeft = this.GetBottomBorderPositionLeft();
                positionRight = this.GetBottomBorderPositionRight();
                width = this.GetWidth();
                cell = this.BorderCell;
            }
            else if (element == "borderLeft")
            {
                positionTop = this.GetLeftBorderPositionTop();
                positionBottom = this.GetLeftBorderPositionBottom();
                positionLeft = this.GetLeftBorderPositionLeft();
                positionRight = this.GetLeftBorderPositionRight();
                height = this.GetLeftBorderHeight();
                cell = this.BorderCell;
            }
            else if (element == "borderRight")
            {
                positionTop = this.GetRightBorderPositionTop();
                positionBottom = this.GetRightBorderPositionBottom();
                positionLeft = this.GetRightBorderPositionLeft();
                positionRight = this.GetRightBorderPositionRight();
                height = this.GetRightBorderHeight();
                cell = this.BorderCell;
            }
            else if (element == "paddingTop")
            {
                positionTop = this.GetTopPaddingPositionTop();
                positionBottom = this.GetTopPaddingPositionBottom();
                positionLeft = this.GetTopPaddingPositionLeft();
                positionRight = this.GetTopPaddingPositionRight();
                width = this.GetTopPaddingWidth();
                cell = this.PaddingCellTop;
            }
            else if (element == "paddingBottom")
            {
                positionTop = this.GetBottomPaddingPositionTop();
                positionBottom = this.GetBottomPaddingPositionBottom();
                positionLeft = this.GetBottomPaddingPositionLeft();
                positionRight = this.GetBottomPaddingPositionRight();
                width = this.GetTopPaddingWidth();
                cell = this.PaddingCellBottom;
            }
            else if (element == "paddingLeft")
            {
                positionTop = this.GetLeftPaddingPositionTop();
                positionBottom = this.GetLeftPaddingPositionBottom();
                positionLeft = this.GetLeftPaddingPositionLeft();
                positionRight = this.GetLeftPaddingPositionRight();
                height = this.GetLeftPaddingHeight();
                cell = this.PaddingCellLeft;
            }
            else if (element == "paddingRight")
            {
                positionTop = this.GetRightPaddingPositionTop();
                positionBottom = this.GetRightPaddingPositionBottom();
                positionLeft = this.GetRightPaddingPositionLeft();
                positionRight = this.GetRightPaddingPositionRight();
                height = this.GetRightPaddingHeight();
                cell = this.PaddingCellRight;
            }
            else if (element.ToLower().Contains("item"))
            {
                positionTop = this.GetItemPositionTop(itemNumber);
                positionBottom = this.GetItemPositionBottom(itemNumber);
                positionLeft = this.GetItemPositionLeft();
                positionRight = this.GetItemPositionRight();
                width = this.GetItemWidth();
                cell = this.PaddingCellBottom;
            }

            var coordinates = new Coordinates(positionLeft, positionTop); // x, y
            var rectangle = new Rectangle(
                positionLeft, positionTop, positionRight, positionBottom); // left, top, right, bottom
            var bufferContent = this.Buffer.PSHost.UI.RawUI.GetBufferContents(rectangle);
            var size = new Size(width, height);
            var bufferContentNew = this.Buffer.PSHost.UI.RawUI.NewBufferCellArray(size, cell);
            var bufferCellElement = new BufferCellElement(bufferContent, bufferContentNew, 
                                                            coordinates, control);
            if (element == "item")
            {
                bufferContentNew = this.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), this.ForegroundColor, this.BackgroundColor);
                bufferCellElement = new BufferCellElement(
                    bufferContent, bufferContentNew, coordinates, control, obj);
            }
            else if (element == "activeItem")
            {
                bufferContentNew = this.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), this.ActiveItemForegroundColor, this.BackgroundColor);
                bufferCellElement = new BufferCellElement(
                    bufferContent, bufferContentNew, coordinates, control, obj);
            }
            else if (element == "selectedItem")
            {
                bufferContentNew = this.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), this.SelectedItemForegroundColor, this.BackgroundColor);
                bufferCellElement = new BufferCellElement(
                    bufferContent, bufferContentNew, coordinates, control, obj);
            }
            else if (element == "activeAndSelectedItem")
            {
                bufferContentNew = this.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), this.ActiveAndSelectedItemForegroundColor, this.BackgroundColor);
                bufferCellElement = new BufferCellElement(
                    bufferContent, bufferContentNew, coordinates, control, obj);
            }
            
            return bufferCellElement;
        }

        protected List<BufferCellElement> GetPSHostRawUIBorderRepresentation()
        {
            var bufferCellElement = new List<BufferCellElement>();
            var bufferCellElementHorizontal = new List<BufferCellElement>();
            var bufferCellElementVertical = new List<BufferCellElement>();

            bool IsHorizontalListFull()
            {
                if (bufferCellElementHorizontal.Count >= this.GetHeight())
                    return true;
                return false;
            }

            bool IsVerticalListFull()
            {
                if (bufferCellElementVertical.Count >= this.GetWidth())
                    return true;
                return false;
            }

            // Top border
            var topBorderBCE = this.NewBufferCellElement("borderTop", control: this);            

            // Bottom border
            var bottomBorderBCE = this.NewBufferCellElement("borderBottom", control: this);

            // Left border
            var leftBorderBCE = this.NewBufferCellElement("borderLeft", control: this);

            // Right border
            var rightBorderBCE = this.NewBufferCellElement("borderRight", control: this);

            // Top padding
            var topPaddingBCE = this.NewBufferCellElement("paddingTop", control: this);

            // Bottom padding
            var bottomPaddingBCE = this.NewBufferCellElement("paddingBottom", control: this);

            // Left padding
            var leftPaddingBCE = this.NewBufferCellElement("paddingLeft", control: this);

            // Right padding
            var rightPaddingBCE = this.NewBufferCellElement("paddingRight", control: this);

            if (this.BorderTop && !IsHorizontalListFull())
                bufferCellElementHorizontal.Add(topBorderBCE);
            if (this.BorderBottom && !IsHorizontalListFull())
                bufferCellElementHorizontal.Add(bottomBorderBCE);
            if (this.PaddingTop && !IsHorizontalListFull())
                bufferCellElementHorizontal.Add(topPaddingBCE);
            if (this.PaddingBottom && !IsHorizontalListFull())
                bufferCellElementHorizontal.Add(bottomPaddingBCE);
            
            foreach (BufferCellElement bce in bufferCellElementHorizontal)
                bufferCellElement.Add(bce);

            if (this.BorderLeft && !IsVerticalListFull())
                bufferCellElementVertical.Add(leftBorderBCE);
            if (this.BorderRight && !IsVerticalListFull())
                bufferCellElementVertical.Add(rightBorderBCE);
            if (this.PaddingLeft && !IsVerticalListFull())
                bufferCellElementVertical.Add(leftPaddingBCE);
            if (this.PaddingRight && !IsVerticalListFull())
                bufferCellElementVertical.Add(rightPaddingBCE);
            
            foreach (BufferCellElement bce in bufferCellElementVertical)
                bufferCellElement.Add(bce);
            
            return bufferCellElement;
        }

        internal void SetObjectBufferCellElement(Object findItem, ConsoleColor foregroundColor, 
                                            ConsoleColor backgroundColor)
        {
            var bce = this.Buffer.GetBufferCellElement(findItem);
            if (bce != null)
            {
                List<string> text = new List<string>();
                var txt = findItem.ToString().PadRight(this.GetItemHorizontalSpace(), this.FillCharacter);
                text.Add(txt);
                bce.NewBufferCellArray = this.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), foregroundColor, backgroundColor);
                bce.Changed = true;
            }
        }
    }

    class Container : Control
    {
        public bool Vertical { get; set; } = false;
        public bool SetContainerToWidestControlWidth { get; set; } = true;
        public bool SetControlsToContainerWidth { get; set; } = true;
        public bool AutoPositionControls { get; set; } = true;
        public bool SetContainerToCombinedControlHeight { get; set; } = true;

        public List<Control> controls = new List<Control>();

        public Container(int left, int top, int width, int height)
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(width);
            this.SetHeight(height);
        }

        public void SetControlsWidth(int width)
        {
            foreach (Control control in this.controls)
                control.SetWidth(width);
        }

        public int GetWidestControlWidth()
        {
            int i = 0;
            foreach (Control control in this.controls)
            {
                int y = control.GetWidth();
                if (y > i)
                {
                    i = y;
                }
            }
            return i;
        }

        public void AddControl(Control control)
        {
            control.Container = this;
            control.Buffer = this.Buffer;

            if (this.SetContainerToCombinedControlHeight)
                this.SetHeight(Console.WindowHeight);

            if (this.SetContainerToWidestControlWidth)
            {
                this.SetWidth(control.GetWidth());
                if (this.SetControlsToContainerWidth)
                    // Set existing member controls to the changed width
                    SetControlsWidth(this.GetWidth());
            }

            if (this.SetControlsToContainerWidth)
                control.SetWidth(this.GetWidth());

            if (controls.Count == 0 && this.AutoPositionControls)
            {
                // First control to be added, set position to this containers top left
                control.SetHorizontalPosition(this.Position.X);
                control.SetVerticalPosition(this.Position.Y);
            }
            else if (this.AutoPositionControls)
            {
                // Get the last controls coordinates and position the new one after it
                if (!Vertical)
                {
                    var lastControl = controls[controls.Count - 1];
                    var left = this.Position.X;
                    var top = lastControl.GetBottomEdgePosition();
                    control.SetHorizontalPosition(left);
                    control.SetVerticalPosition(top);
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            if (control.GetBottomEdgePosition() > this.GetBottomEdgePosition())
                control.SetBottomEdgePosition(this.GetBottomEdgePosition());

            // TODO control.Container becomes inaccessible unless I make it public, this is not good.
            //      There's an explanation over yonder:
            //      https://stackoverflow.com/questions/567705/why-cant-i-access-c-sharp-protected-members-except-like-this
            controls.Add(control);

            if (this.SetContainerToCombinedControlHeight)
            {
                var height = 0;
                foreach (Control ctrl in this.controls)
                {
                    height += ctrl.GetHeight();
                }
                this.SetHeight(height);
            }
        }

        public void RemoveControl(Control control)
        {
            throw new NotImplementedException();
        }

        public new void SetWidth(int width)
        {
            if (width < 0)
                width = 0;

            this.width = width;

            if (this.SetControlsToContainerWidth)
                // Set existing member controls to the new width
                SetControlsWidth(this.GetWidth());
        }

        public new void SetHorizontalPosition(int x)
        {
            Dictionary<Control, int> newControlPositions = new Dictionary<Control, int>();
            
            var numberOfColumns = 0;
            var direction = "left";

            if (x > this.Position.X)
            {
                numberOfColumns = x - this.Position.X;
                direction = "right";
            }
            else if (x < this.Position.X)
                numberOfColumns = this.Position.X - x;
            else
                return;

            // Determine a new position for each contained Control
            foreach (Control control in this.controls)
            {
                var newX = control.Position.X;
                if (direction == "right")
                    newX += numberOfColumns;
                else
                    newX -= numberOfColumns;

                newControlPositions.Add(control, newX);
            }

            // Move this Container
            base.SetHorizontalPosition(x);
            
            // Move each Control to its new position
            foreach (var newPos in newControlPositions)
            {
                newPos.Key.SetHorizontalPosition(newPos.Value);
            }
        }

        public new void SetVerticalPosition(int y)
        {
            Dictionary<Control, int> newControlPositions = new Dictionary<Control, int>();
            
            var numberOfRows = 0;
            var direction = "up";

            if (y > this.Position.Y)
            {
                numberOfRows = y - this.Position.Y;
                direction = "down";
            }
            else if (y < this.Position.Y)
                numberOfRows = this.Position.Y - y;
            else
                return;

            // Determine a new position for each contained Control
            foreach (Control control in this.controls)
            {
                var newY = control.Position.Y;
                if (direction == "down")
                    newY += numberOfRows;
                else
                    newY -= numberOfRows;

                newControlPositions.Add(control, newY);
            }

            // Move this Container
            base.SetVerticalPosition(y);
            
            // Move each Control to its new position
            foreach (var newPos in newControlPositions)
            {
                newPos.Key.SetVerticalPosition(newPos.Value);
            }
        }

        public override List<string> GetTextRepresentation()
        {
            var text = new List<string>();

            foreach (Control control in this.controls)
            {
                foreach (string txt in control.GetTextRepresentation())
                {
                    text.Add(txt);
                }
            }

            return text;
        }

        public override List<BufferCellElement> GetPSHostRawUIRepresentation()
        {
            throw new NotImplementedException();
        }
    }

    class Label : Control
    {
        private List<string> Text { get; set; } = new List<string>();

        public Label(int left, int top, string text) : base()
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(text.Length);
            this.SetHeight(1);
            this.Text.Add(text);
        }

        public Label(int left, int top, List<string> text) : base()
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            foreach (string txt in text)
            {
                if (this.GetWidth() < txt.Length)
                    this.SetWidth(txt.Length);
            }
            this.SetHeight(text.Count);
            this.Text = text;
        }

        public Label(int left, int top, int width, int height) : base()
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(width);
            this.SetHeight(height);
        }
        
        public override List<string> GetTextRepresentation()
        {
            var outText = new List<string>();
            var text = this.Text;
            var horizontalBorder = new string(this.BorderCharacter, this.GetWidth());
            var count = this.GetWidth() - 2;
            if (count < 0)
                count = 0;
            var emptyLine = new String(this.FillCharacter, count);
            var textHorizontalSpace = this.GetItemHorizontalSpace();

            if (this.GetWidth() == 0 || this.GetHeight() == 0)
            {
                outText.Add("");
                return outText;
            }

            if (this.GetWidth() + this.GetHeight() == 2)
            {
                // There's only room for one character
                if (this.BorderTop || this.BorderRight || this.BorderBottom || this.BorderLeft)
                    // And it will be a border one
                    outText.Add(this.BorderCharacter.ToString());
                else
                    outText.Add(text[0].Substring(0,1));
                return outText;
            }

            if (this.BorderTop)
            {
                outText.Add(horizontalBorder);
                if (this.GetHeight() == 1)
                {
                    return outText;
                }
                else if (this.GetHeight() == 2 && this.BorderBottom)
                {
                    outText.Add(horizontalBorder);
                    return outText;
                }
            }

            foreach (string txt in text)
            {
                string outTxt = txt;
                int leftFillCount = 0; // Number of fill characters on left side of text

                if (this.AlignText == "center" && outTxt.Length < textHorizontalSpace)
                {
                    leftFillCount = (textHorizontalSpace - outTxt.Length) / 2;
                    outTxt = new String(this.FillCharacter, leftFillCount) + outTxt;
                }

                if (outTxt.Length > textHorizontalSpace)
                    outTxt = outTxt.Substring(0, textHorizontalSpace);
                else
                    outTxt = outTxt.PadRight(textHorizontalSpace, this.FillCharacter);

                if (this.BorderLeft && this.BorderRight && this.GetWidth() == 2)
                {
                    outTxt = new string(this.BorderCharacter, 2);
                    emptyLine = new string(this.BorderCharacter, 2);
                }
                else if ((this.BorderLeft || this.BorderRight) && this.GetWidth() == 1)
                {
                    outTxt = new string(this.BorderCharacter, 1);
                    emptyLine = new string(this.BorderCharacter, 1);
                }

                else
                {
                    if (this.BorderRight && this.PaddingRight)
                    {
                        outTxt = outTxt + this.PaddingCharacterRight + this.BorderCharacter;
                        emptyLine = emptyLine + this.BorderCharacter;
                    }
                    else if (this.BorderRight)
                    {
                        outTxt = outTxt + this.BorderCharacter;
                        emptyLine = emptyLine + this.BorderCharacter;
                    }
                    else if (this.PaddingRight)
                    {
                        outTxt = outTxt + this.PaddingCharacterRight;
                        emptyLine = emptyLine + this.PaddingCharacterRight;
                    }
                    else
                        emptyLine = emptyLine + this.FillCharacter;
                    
                    if (this.BorderLeft && this.PaddingLeft)
                    {
                        outTxt = "" + this.BorderCharacter + this.PaddingCharacterLeft + outTxt;
                        emptyLine = "" + this.BorderCharacter + emptyLine;
                    }
                    else if (this.BorderLeft)
                    {
                        outTxt = this.BorderCharacter + outTxt;
                        emptyLine = this.BorderCharacter + emptyLine;
                    }
                    else if (this.PaddingLeft)
                    {
                        outTxt = this.PaddingCharacterLeft + outTxt;
                        emptyLine = this.PaddingCharacterLeft + emptyLine;
                    }
                    else
                        emptyLine = this.FillCharacter + emptyLine;

                    if (!this.BorderRight && !this.BorderLeft && this.GetWidth() == 1)
                        emptyLine = "" + this.FillCharacter;
                }

                if (this.PaddingTop)
                    outText.Add(emptyLine.Replace(this.FillCharacter, this.PaddingCharacterTop));

                outText.Add(outTxt);
            }

            var numberOfEmptyLines = this.GetHeight();

            if (this.BorderTop)
                numberOfEmptyLines--;

            if (this.BorderBottom)
                numberOfEmptyLines--;

            if (this.PaddingTop)
                numberOfEmptyLines--;

            if (this.PaddingBottom)
                numberOfEmptyLines--;

            if (numberOfEmptyLines > 1)
            {
                for (var i = 1; i < numberOfEmptyLines; i++)
                {
                    outText.Add(emptyLine);
                }
            }

            if (this.PaddingTop)
                outText.Add(emptyLine.Replace(this.FillCharacter, this.PaddingCharacterBottom));
            
            if (this.BorderBottom)
                outText.Add(horizontalBorder);

            return outText;
        }
        
        public override List<BufferCellElement> GetPSHostRawUIRepresentation()
        {
            UpdatePSHostVariables();

            var textHorizontalSpace = this.GetItemHorizontalSpace();
            List<string> content = new List<string>();
            List<string> text = this.Text;
            foreach (string txt in text)
            {
                string outTxt = txt;
                int leftFillCount = 0; // Number of fill characters on left side of text
                if (outTxt.Length == 0)
                    outTxt = " ";
                if (this.AlignText == "center" && outTxt.Length < textHorizontalSpace)
                {
                    leftFillCount = (textHorizontalSpace - outTxt.Length) / 2;
                    outTxt = new String(this.FillCharacter, leftFillCount) + outTxt;
                }
                outTxt = outTxt.PadRight(textHorizontalSpace, this.FillCharacter);
                content.Add(outTxt);
            }

            var bufferCellElement = new List<BufferCellElement>();
            foreach (BufferCellElement bce in this.GetPSHostRawUIBorderRepresentation())
                bufferCellElement.Add(bce);
            var numberOfUsedContentRows = 0;

            int GetNumberOfAvailableContentRows()
            {
                var rows = this.GetHeight();
                rows -= numberOfUsedContentRows;
                if (this.BorderTop)
                    rows = rows - 1;
                if (this.BorderBottom)
                    rows = rows - 1;
                if (this.PaddingTop)
                    rows = rows - 1;
                if (this.PaddingBottom)
                    rows = rows - 1;
                return rows;
            }
            
            bufferCellElement.Add(NewBufferCellElement("item", content));

            if (GetNumberOfAvailableContentRows() > content.Count)
            {
                string spaces = new String(this.FillCharacter, textHorizontalSpace);
                for (int i = 1; i < GetNumberOfAvailableContentRows(); i++)
                    content.Add(spaces);
                bufferCellElement.Add(NewBufferCellElement("item", content));
            }

            return bufferCellElement;
        }
    }

    class TextBox : Control
    {
        public string Text { get; set; }
        public int CursorPositionTop { get; set; }
        public int CursorPositionLeft { get; set; }

        public TextBox(int left, int top, string text) : base()
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(text.Length);
            this.SetHeight(1);
            this.Text = text;
        }

        public TextBox(int left, int top, int width) : base()
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(width);
            this.SetHeight(1);
            this.Text = " ";
        }

        public new void SetHorizontalPosition(int x)
        {
            if (this.Container != null)
            {
                if (x + this.GetWidth() > this.Container.GetRightEdgePosition())
                {
                    this.Position = new Coordinates(x, this.Position.Y);
                    this.SetRightEdgePosition(this.Container.GetRightEdgePosition());
                    return;
                }

                if (x < this.Container.GetLeftEdgePosition())
                {
                    this.SetLeftEdgePosition(this.Container.GetLeftEdgePosition());
                    // Remove the number of characters passing left side of the Container
                    width = this.GetWidth();
                    width = width - (this.Container.GetLeftEdgePosition() - x);
                    this.SetWidth(width);
                    return;
                }
            }
            
            this.CursorPositionLeft = x;

            if (this.BorderLeft)
                this.CursorPositionLeft += 1;
            if (this.PaddingLeft)
                this.CursorPositionLeft += 1;

            this.Position = new Coordinates(x, this.Position.Y);
        }

        public new void SetVerticalPosition(int y)
        {
            if (this.Container != null)
            {
                if (y + this.GetHeight() > this.Container.GetBottomEdgePosition())
                {
                    this.Position = new Coordinates(this.Position.X, y);
                    this.SetBottomEdgePosition(this.Container.GetBottomEdgePosition());
                    return;
                }

                if (y < this.Container.GetTopEdgePosition())
                {
                    this.SetTopEdgePosition(this.Container.GetTopEdgePosition());
                    // Remove the number of characters passing top side of the Container
                    height = this.GetHeight();
                    height = height - (this.Container.GetTopEdgePosition() - y);
                    this.SetHeight(height);
                    return;
                }
            }

            this.Position = new Coordinates(this.Position.X, y);
        }

        public int GetLastLegalCursorPosition()
        {
            var lastLegalPos = this.GetRightEdgePosition();

            if (this.BorderLeft)
                lastLegalPos--;
            
            if (this.PaddingLeft)
                lastLegalPos--;

            return lastLegalPos;
        }

        public string ReadKey()
        {
            this.Text = this.Text.Trim();
            this.CursorPositionTop = this.Position.Y;

            if (this.BorderTop)
                this.CursorPositionTop += 1;
            if (this.PaddingTop)
                this.CursorPositionTop += 1;

            this.CursorPositionLeft = this.Position.X;

            if (this.BorderLeft)
                this.CursorPositionLeft += 1;
            if (this.PaddingLeft)
                this.CursorPositionLeft += 1;
            
            Console.SetCursorPosition(this.CursorPositionLeft,this.CursorPositionTop);
            ConsoleKeyInfo key = Console.ReadKey(true);
            while (key.Key != ConsoleKey.Enter)
            {
                if (key.Key == ConsoleKey.Backspace)
                {
                    int cursorPosX = Console.CursorLeft;
                    if (cursorPosX > this.CursorPositionLeft)
                    {
                        Console.SetCursorPosition(cursorPosX - 1, this.CursorPositionTop);
                        Console.Write(this.FillCharacter);
                        Console.SetCursorPosition(cursorPosX - 1, this.CursorPositionTop);
                    }
                } else { 
                    // TODO Allow the text to scroll instead of only allowing to input until the 
                    // right edge is reached:
                    if (Console.CursorLeft + 1 <= this.GetLastLegalCursorPosition())
                    {
                        this.Text += key.KeyChar;
                        Console.Write(key.KeyChar);
                    }
                }
                key = Console.ReadKey(true);
            }
            return this.Text;
        }

        public override List<string> GetTextRepresentation()
        {
            var text = new List<string>();
            var txt = this.Text;
            var horizontalBorder = new string(this.BorderCharacter, this.GetWidth());
            var count = this.GetWidth() - 2;
            if (count < 0)
                count = 0;
            var emptyLine = new String(this.FillCharacter, count);
            var textHorizontalSpace = this.GetItemHorizontalSpace();

            if (this.GetWidth() == 0 || this.GetHeight() == 0)
            {
                text.Add("");
                return text;
            }

            if (this.GetWidth() + this.GetHeight() == 2)
            {
                // There's only room for one character
                if (this.BorderTop || this.BorderRight || this.BorderBottom || this.BorderLeft)
                    // And it will be a border one
                    text.Add(this.BorderCharacter.ToString());
                else
                    text.Add(txt.Substring(0,1));
                return text;
            }

            if (this.BorderTop)
            {
                text.Add(horizontalBorder);
                if (this.GetHeight() == 1)
                {
                    return text;
                }
                else if (this.GetHeight() == 2 && this.BorderBottom)
                {
                    text.Add(horizontalBorder);
                    return text;
                }
            }

            if (txt.Length > textHorizontalSpace)
                txt = txt.Substring(0, textHorizontalSpace);
            else
                txt = txt.PadRight(textHorizontalSpace, this.FillCharacter);

            if (this.BorderLeft && this.BorderRight && this.GetWidth() == 2)
            {
                txt = new string(this.BorderCharacter, 2);
                emptyLine = new string(this.BorderCharacter, 2);
            }
            else if ((this.BorderLeft || this.BorderRight) && this.GetWidth() == 1)
            {
                txt = new string(this.BorderCharacter, 1);
                emptyLine = new string(this.BorderCharacter, 1);
            }
            else
            {
                if (this.BorderRight && this.PaddingRight)
                {
                    txt = txt + this.PaddingCharacterRight + this.BorderCharacter;
                    emptyLine = emptyLine + this.BorderCharacter;
                }
                else if (this.BorderRight)
                {
                    txt = txt + this.BorderCharacter;
                    emptyLine = emptyLine + this.BorderCharacter;
                }
                else if (this.PaddingRight)
                {
                    txt = txt + this.PaddingCharacterRight;
                    emptyLine = emptyLine + this.PaddingCharacterRight;
                }
                else
                    emptyLine = emptyLine + this.FillCharacter;
                
                if (this.BorderLeft && this.PaddingLeft)
                {
                    txt = "" + this.BorderCharacter + this.PaddingCharacterLeft + txt;
                    emptyLine = "" + this.BorderCharacter + emptyLine;
                }
                else if (this.BorderLeft)
                {
                    txt = this.BorderCharacter + txt;
                    emptyLine = this.BorderCharacter + emptyLine;
                }
                else if (this.PaddingLeft)
                {
                    txt = this.PaddingCharacterLeft + txt;
                    emptyLine = this.PaddingCharacterLeft + emptyLine;
                }
                else
                    emptyLine = this.FillCharacter + emptyLine;

                if (!this.BorderRight && !this.BorderLeft && this.GetWidth() == 1)
                    emptyLine = "" + this.FillCharacter;
            }

            if (this.PaddingTop)
                text.Add(emptyLine.Replace(this.FillCharacter, this.PaddingCharacterTop));

            text.Add(txt);

            var numberOfEmptyLines = this.GetHeight();

            if (this.BorderTop)
                numberOfEmptyLines--;

            if (this.BorderBottom)
                numberOfEmptyLines--;

            if (this.PaddingTop)
                numberOfEmptyLines--;

            if (this.PaddingBottom)
                numberOfEmptyLines--;

            if (numberOfEmptyLines > 1)
            {
                for (var i = 1; i < numberOfEmptyLines; i++)
                {
                    text.Add(emptyLine);
                }
            }

            if (this.PaddingTop)
                text.Add(emptyLine.Replace(this.FillCharacter, this.PaddingCharacterBottom));
            
            if (this.BorderBottom)
                text.Add(horizontalBorder);

            return text;
        }
        
        public override List<BufferCellElement> GetPSHostRawUIRepresentation()
        {
            UpdatePSHostVariables();

            var textHorizontalSpace = this.GetItemHorizontalSpace();
            List<string> content = new List<string>();
            string txt = this.Text;
            if (txt.Length == 0)
                txt = " ";
            txt = txt.PadRight(textHorizontalSpace, this.FillCharacter);
            content.Add(txt);

            var bufferCellElement = new List<BufferCellElement>();
            foreach (BufferCellElement bce in this.GetPSHostRawUIBorderRepresentation())
                bufferCellElement.Add(bce);
            var numberOfUsedContentRows = 0;

            int GetNumberOfAvailableContentRows()
            {
                var rows = this.GetHeight();
                rows -= numberOfUsedContentRows;
                if (this.BorderTop)
                    rows = rows - 1;
                if (this.BorderBottom)
                    rows = rows - 1;
                if (this.PaddingTop)
                    rows = rows - 1;
                if (this.PaddingBottom)
                    rows = rows - 1;
                return rows;
            }
            
            if (GetNumberOfAvailableContentRows() == 1)
                bufferCellElement.Add(NewBufferCellElement("item", content));
            else if (GetNumberOfAvailableContentRows() > 1)
            {
                string spaces = new String(this.FillCharacter, this.Text.Length);
                for (int i = 1; i < GetNumberOfAvailableContentRows(); i++)
                    content.Add(spaces);
                bufferCellElement.Add(NewBufferCellElement("item", content));
            }

            return bufferCellElement;
        }
    }

    class Menu : Control
    {
        private List<Object> Objects { get; set; } = new List<Object>();
        private int TopDisplayedObjectIndex { get; set; } = 0;
        private int BottomDisplayedObjectIndex { get; set; } = 0;
        private List<Object> DisplayedObjects { get; set; } = new List<Object>();
        private List<Object> SelectedObjects { get; set; } = new List<Object>();
        private Object ActiveObject { get; set; } = 0; // Highlighted object
        public string Mode { get; set; } = "Default";

        public Menu(int left, int top, List<Object> objects) : base()
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            for (var i = 0; i < objects.Count; i++)
            {
                var objLength = objects[i].ToString().Length;
                if (objLength > this.GetWidth())
                {
                    this.SetWidth(objLength);
                }
            }
            this.SetHeight(objects.Count);
            this.Objects = objects;
            this.SetItemActive(0);
        }

        private int GetNumberOfAvailableRowsForItems()
        {
            // Returns the number of rows that can fit items on the menu
            var rows = this.GetHeight();
            if (this.BorderTop)
                rows--;
            if (this.BorderBottom)
                rows--;
            if (this.PaddingTop)
                rows--;
            if (this.PaddingBottom)
                rows--;

            if (rows < 0)
                rows = 0;

            return rows;
        }

        public List<Object> ReadKey()
        {
            var searchTerm = "";
            var item = this.ActiveObject;
            while (true)
            {
                var key = Console.ReadKey(true).Key; // true hides key strokes
                switch (key.ToString())
                {
                    case KeyUp0:
                    case KeyUp1:
                        if (this.Mode == "List")
                            break;
                        this.SetPreviousItemActive();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyDown0:
                    case KeyDown1:
                        if (this.Mode == "List")
                            break;
                        this.SetNextItemActive();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyPageUp:
                        this.LoadPreviousPage();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyPageDown:
                        this.LoadNextPage();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyFind:
                        if (this.Mode == "List")
                            break;
                        var searchBox = new TextBox(0, 0, 0);
                        searchBox.AddBorder("all");
                        searchBox.AddPadding("all");
                        searchBox.SetWidth(this.GetWidth());

                        var x = this.GetLeftEdgePosition();
                        searchBox.SetHorizontalPosition(x);

                        var y = this.GetHeight() / 2 - searchBox.GetHeight() / 2;
                        searchBox.SetVerticalPosition(y);
                        this.Buffer.Add(searchBox);  // TODO : Maybe make it possible to float the 
                                                     // control in the same container instead.
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        searchTerm = searchBox.ReadKey();
                        this.Buffer.Remove(searchBox);
                        this.Buffer.Write();
                        // Use a try-catch block to silence mangled regexes
                        try
                        {
                            item = this.FindNextItem(searchTerm);
                        }
                        catch
                        {
                            // TODO try basic string matching instead
                            item = null;
                        }
                        if (item != null)
                            this.SetItemActive(this.Objects.IndexOf(item));
                        if (this.Objects.Count > this.GetNumberOfAvailableRowsForItems() &&
                            !this.DisplayedObjects.Contains(item))
                            this.MoveActiveObjectToMiddle();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyFindNext:
                        if (this.Mode == "List")
                            break;
                        item = this.FindNextItem(searchTerm);
                        if (item != null)
                            this.SetItemActive(this.Objects.IndexOf(item));
                        if (this.Objects.Count > this.GetNumberOfAvailableRowsForItems() &&
                            !this.DisplayedObjects.Contains(item))
                            this.MoveActiveObjectToMiddle();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyFindPrevious:
                        if (this.Mode == "List")
                            break;
                        item = this.FindPreviousItem(searchTerm);
                        if (item != null)
                            this.SetItemActive(this.Objects.IndexOf(item));
                        if (this.Objects.Count > this.GetNumberOfAvailableRowsForItems() &&
                            !this.DisplayedObjects.Contains(item))
                            this.MoveActiveObjectToMiddle();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeySelect:
                        if (this.Mode == "List")
                            return this.Objects;
                        if (this.SelectedObjects.Contains(this.ActiveObject))
                            this.RemoveSelectedObject(this.ActiveObject);
                        else
                            this.AddSelectedObject(this.ActiveObject);
                        if (this.Mode == "Default")
                            return this.SelectedObjects;
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyConfirm:
                        if (this.Mode == "List")
                            return this.Objects;
                        if (this.Mode == "Default")
                            this.AddSelectedObject(this.ActiveObject);
                        return this.SelectedObjects;
                    case KeyCancel:
                        return null;
                }
            }
        }

        private void SetNextItemActive()
        {
            var activeObjectIndex = this.Objects.IndexOf(this.ActiveObject);

            if (this.Objects.Count > this.GetNumberOfAvailableRowsForItems())
            {
                if (activeObjectIndex + 1 < this.Objects.Count)
                    this.SetItemActive(activeObjectIndex + 1);
                else
                    this.SetItemActive(0);

                if (!this.DisplayedObjects.Contains(this.ActiveObject))
                    this.LoadNextPage();
            }
            else
            {
                if (activeObjectIndex + 1 < this.Objects.Count)
                    this.SetItemActive(activeObjectIndex + 1);
                else
                    this.SetItemActive(0);
            }
        }

        private void SetPreviousItemActive()
        {
            var activeObjectIndex = this.Objects.IndexOf(this.ActiveObject);

            if (this.Objects.Count > this.GetNumberOfAvailableRowsForItems())
            {
                if (activeObjectIndex - 1 >= 0)
                    this.SetItemActive(activeObjectIndex - 1);
                else
                    this.SetItemActive(this.Objects.Count - 1);

                if (!this.DisplayedObjects.Contains(this.ActiveObject))
                    this.LoadPreviousPage();
            }
            else
            {
                if (activeObjectIndex - 1 >= 0)
                    this.SetItemActive(activeObjectIndex - 1);
                else
                    this.SetItemActive(this.Objects.Count - 1);
            }
        }

        private Object FindNextItem(string searchTerm)
        {
            searchTerm = searchTerm.ToLower();
            Regex regex = new Regex(searchTerm);
            var index = this.Objects.IndexOf(this.ActiveObject);
            var firstIndex = index;
            while (true)
            {
                index++;
                if (index > this.Objects.Count - 1)
                    index = 0;

                if (index == firstIndex)
                    return null;

                var control = this.Objects[index];
                Match match = regex.Match(control.ToString().ToLower());
                if (match.Success)
                    return control;
            }
        }

        private Object FindPreviousItem(string searchTerm)
        {
            searchTerm = searchTerm.ToLower();
            Regex regex = new Regex(searchTerm);
            var index = this.Objects.IndexOf(this.ActiveObject);
            var firstIndex = index;
            while (true)
            {
                index--;
                if (index < 0)
                    index = this.Objects.Count - 1;

                if (index == firstIndex)
                    return null;

                var control = this.Objects[index];
                Match match = regex.Match(control.ToString().ToLower());
                if (match.Success)
                    return control;
            }
        }

        private void SetItemActive(int itemIndex)
        {
            if (this.Mode == "List")
                return;
            
            ConsoleColor newForegroundColor;

            if (this.SelectedObjects.Contains(this.ActiveObject))
                newForegroundColor = this.SelectedItemForegroundColor;
            else
                newForegroundColor = this.ForegroundColor;

            if (this.Buffer != null && this.Buffer.PSHost != null)
                this.SetObjectBufferCellElement(this.ActiveObject, newForegroundColor, this.BackgroundColor);

            this.ActiveObject = this.Objects[itemIndex];

            if (this.SelectedObjects.Contains(this.ActiveObject))
                newForegroundColor = this.ActiveAndSelectedItemForegroundColor;
            else
                newForegroundColor = this.ActiveItemForegroundColor;

            if (this.Buffer != null && this.Buffer.PSHost != null)
                this.SetObjectBufferCellElement(this.ActiveObject, newForegroundColor, this.BackgroundColor);
        }

        private void AddSelectedObject(Object item)
        {
            ConsoleColor newForegroundColor;

            if (!this.SelectedObjects.Contains(item))
                this.SelectedObjects.Add(item);

            if (this.ActiveObject == item)
                newForegroundColor = this.ActiveAndSelectedItemForegroundColor;
            else
                newForegroundColor = this.SelectedItemForegroundColor;

            if (this.Buffer != null && this.Buffer.PSHost != null)
                this.SetObjectBufferCellElement(this.ActiveObject, newForegroundColor, this.BackgroundColor);
        }

        private void RemoveSelectedObject(Object item)
        {
            ConsoleColor newForegroundColor;

            if (this.SelectedObjects.Contains(item))
                this.SelectedObjects.Remove(item);

            if (this.ActiveObject == item)
                newForegroundColor = this.ActiveItemForegroundColor;
            else
                newForegroundColor = this.ForegroundColor;

            if (this.Buffer != null && this.Buffer.PSHost != null)
                this.SetObjectBufferCellElement(this.ActiveObject, newForegroundColor, this.BackgroundColor);
        }

        private void LoadNextPage()
        {
            this.TopDisplayedObjectIndex = this.BottomDisplayedObjectIndex + 1;
            if (this.TopDisplayedObjectIndex == this.Objects.Count)
                this.TopDisplayedObjectIndex = 0;
            this.Buffer.Clear();
            if (this.Mode != "List")
                this.ActiveObject = this.Objects[this.TopDisplayedObjectIndex];
        }

        private void LoadPreviousPage()
        {
            int newTopDisplayedObjectIndex = this.TopDisplayedObjectIndex;

            for (int i = 1; i <= this.GetNumberOfAvailableRowsForItems(); i++)
            {
                newTopDisplayedObjectIndex--;
                if (newTopDisplayedObjectIndex < 0)
                    newTopDisplayedObjectIndex = this.Objects.Count - 1;
            }

            this.TopDisplayedObjectIndex = newTopDisplayedObjectIndex;
            int newBottomDisplayedObjectIndex = this.TopDisplayedObjectIndex;

            for (int i = 1; i <= this.GetNumberOfAvailableRowsForItems() - 1; i++)
            {
                newBottomDisplayedObjectIndex++;
                if (newBottomDisplayedObjectIndex > this.Objects.Count - 1)
                    newBottomDisplayedObjectIndex = 0;
            }

            this.BottomDisplayedObjectIndex = newBottomDisplayedObjectIndex;

            this.Buffer.Clear(); // TODO Not sure this ought to be here...
            if (this.Mode != "List")
                this.ActiveObject = this.Objects[this.BottomDisplayedObjectIndex];
        }

        private void MoveActiveObjectToMiddle()
        {
            int newTopDisplayedObjectIndex = this.Objects.IndexOf(this.ActiveObject);

            for (int i = 1; i <= this.GetNumberOfAvailableRowsForItems() / 2; i++)
            {
                newTopDisplayedObjectIndex--;
                if (newTopDisplayedObjectIndex < 0)
                    newTopDisplayedObjectIndex = this.Objects.Count - 1;
            }

            this.TopDisplayedObjectIndex = newTopDisplayedObjectIndex;
            int newBottomDisplayedObjectIndex = this.TopDisplayedObjectIndex;

            for (int i = 1; i <= this.GetNumberOfAvailableRowsForItems() - 1; i++)
            {
                newBottomDisplayedObjectIndex++;
                if (newBottomDisplayedObjectIndex > this.Objects.Count - 1)
                    newBottomDisplayedObjectIndex = 0;
            }

            this.BottomDisplayedObjectIndex = newBottomDisplayedObjectIndex;

            this.Buffer.Clear(); // TODO Not sure this ought to be here...
        }

        public override List<string> GetTextRepresentation()
        {
            this.DisplayedObjects = new List<Object>();
            var text = new List<string>();
            if (this.GetHeight() == 0)
                return text;
            var textHorizontalSpace = this.GetItemHorizontalSpace();
            var horizontalBorder = new string(this.BorderCharacter, this.GetWidth());
            if ((this.BorderTop || this.BorderBottom) && this.GetHeight() == 1)
            {
                text.Add(horizontalBorder);
                return text;
            }
            var rowsAvailableForItems = this.GetNumberOfAvailableRowsForItems();
            var currentItemIndex = this.TopDisplayedObjectIndex;

            if (this.GetHeight() == 2 && (this.BorderTop && this.BorderBottom))
            {
                text.Add(horizontalBorder);
                text.Add(horizontalBorder);
                return text;
            }
            
            if (this.BorderTop)
                text.Add(horizontalBorder);
            
            var horizontalPadding = new string(this.PaddingCharacterTop, this.GetWidth() - 2);

            if (this.BorderLeft)
                horizontalPadding = this.BorderCharacter + horizontalPadding;
            else
                horizontalPadding = this.PaddingCharacterLeft + horizontalPadding;
            
            if (this.BorderRight)
                horizontalPadding = horizontalPadding + this.BorderCharacter;
            else
                horizontalPadding = horizontalPadding + this.PaddingCharacterRight;
            
            if (this.PaddingTop)
                text.Add(horizontalPadding);
            
            for (var i = 0; i < rowsAvailableForItems; i++)
            {
                if (currentItemIndex > Objects.Count - 1)
                    currentItemIndex = 0;

                var item = this.Objects[currentItemIndex];
                var outTxt = item.ToString();
                int leftFillCount = 0; // Number of fill characters on left side of text

                if (this.AlignText == "center" && outTxt.Length < textHorizontalSpace)
                {
                    leftFillCount = (textHorizontalSpace - outTxt.Length) / 2;
                    outTxt = new String(this.FillCharacter, leftFillCount) + outTxt;
                }

                this.BottomDisplayedObjectIndex = currentItemIndex;
                this.DisplayedObjects.Add(item);

                if (!this.PaddingLeft)
                {
                    if (this.ActiveObject == item && this.SelectedObjects.Contains(item) &&
                        this.Mode != "List")
                        outTxt = "" + this.SelectedAndActiveCharacter + this.FillCharacter + outTxt;
                    else if (this.ActiveObject == item && this.Mode != "List")
                        outTxt = "" + this.ActiveCharacter + this.FillCharacter + outTxt;
                    else if (this.SelectedObjects.Contains(item))
                        outTxt = "" + this.SelectCharacter + this.FillCharacter + outTxt;
                }

                var label = new Label(0, 0, outTxt);
                var width = this.GetWidth();

                if (this.PaddingLeft)
                {
                    if (this.ActiveObject == item && this.SelectedObjects.Contains(item) &&
                        this.Mode != "List")
                        label.PaddingCharacterLeft = this.SelectedAndActiveCharacter;
                    else if (this.ActiveObject == item && this.Mode != "List")
                        label.PaddingCharacterLeft = this.ActiveCharacter;
                    else if (this.SelectedObjects.Contains(item))
                        label.PaddingCharacterLeft = this.SelectCharacter;
                }

                if (this.BorderRight)
                    width--;

                if (this.BorderLeft)
                    width--;

                if (this.PaddingRight)
                    width--;

                if (this.PaddingLeft)
                    width--;

                label.SetWidth(width);

                if (this.BorderRight)
                    label.AddBorder("right");

                if (this.BorderLeft)
                    label.AddBorder("left");
                
                if (this.PaddingRight)
                    label.AddPadding("right");

                if (this.PaddingLeft)
                    label.AddPadding("left");

                foreach (string lblText in label.GetTextRepresentation())
                {
                    text.Add(lblText);
                }

                currentItemIndex++;
            }

            if (this.PaddingBottom)
                text.Add(horizontalPadding);

            if (this.BorderBottom)
                text.Add(horizontalBorder);

            return text;
        }

        public override List<BufferCellElement> GetPSHostRawUIRepresentation()
        {
            UpdatePSHostVariables();
            this.DisplayedObjects = new List<Object>();
            var textHorizontalSpace = this.GetItemHorizontalSpace();

            var bufferCellElement = new List<BufferCellElement>();
            foreach (BufferCellElement bce in this.GetPSHostRawUIBorderRepresentation())
                bufferCellElement.Add(bce);

            var rowsAvailableForItems = this.GetNumberOfAvailableRowsForItems();
            var currentItemIndex = this.TopDisplayedObjectIndex;

            for (var i = 0; i < rowsAvailableForItems; i++)
            {
                if (currentItemIndex > Objects.Count - 1)
                    currentItemIndex = 0;

                this.BottomDisplayedObjectIndex = currentItemIndex;

                var item = this.Objects[currentItemIndex];
                string outTxt = item.ToString();
                int leftFillCount = 0; // Number of fill characters on left side of text
                if (this.AlignText == "center" && outTxt.Length < textHorizontalSpace)
                {
                    leftFillCount = (textHorizontalSpace - outTxt.Length) / 2;
                    outTxt = new String(this.FillCharacter, leftFillCount) + outTxt;
                }
                outTxt = outTxt.PadRight(textHorizontalSpace, this.FillCharacter);
                List<string> content = new List<string>();
                content.Add(outTxt);

                if (item == this.ActiveObject && this.Mode != "List")
                    bufferCellElement.Add(NewBufferCellElement("activeItem", content, i, this, item));
                else
                    bufferCellElement.Add(NewBufferCellElement("item", content, i, this, item));

                this.DisplayedObjects.Add(item);

                currentItemIndex++;
            }

            this.SetObjectBufferCellElement(this.ActiveObject, this.BackgroundColor, this.ForegroundColor);

            return bufferCellElement;
        }
    }
}