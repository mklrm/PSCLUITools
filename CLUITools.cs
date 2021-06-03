using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PSCLUITools
{
    class Buffer : PSCmdlet
    {
        internal int left = Console.WindowLeft;
        internal int top = Console.WindowTop;
        internal Coordinates position { get; set; }

        internal int width = Console.WindowWidth;
        internal int height = Console.WindowHeight;

        protected char[,] screenBufferArray { get; set; }
        internal PSHost PSHost { get; set; }

        internal List<Container> containers = new List<Container>();
        internal List<BufferCellElement> bufferCellElements = new List<BufferCellElement>();
        // List of Controls already added to the buffer. Prevents them from being added multiple times.
        protected List<Control> BufferedControls = new List<Control>();

        internal string Type { get; set; }

        public Buffer()
        {
            this.position = new Coordinates(this.left, this.top);
            this.screenBufferArray = new char[this.width, this.height];
            this.Type = "Console";
        }

        public Buffer(PSHost host)
        {
            this.PSHost = host;
            this.position = new Coordinates(this.left, this.top);
            this.Type = "PSHost";
        }

        internal int GetWidth()
        {
            return width;
        }

        internal int GetHeight()
        {
            int hght = this.height;
            // TODO Sometimes the last line on a console buffer is overwritten by 
            // the command prompt. Lying about the height of the buffer fixes this 
            // but will sometimes obviously produce a Control shortened by one row.
            // Maybe I could move the prompt down by a row or something instead or 
            // at least only lie about the height when needed. Maybe even get rid 
            // of the prompt while running.
            if (this.PSHost == null)
                hght--;
            return hght;
        }

        internal int GetLeftEdgePosition()
        {
            return left;
        }

        internal int GetTopEdgePosition()
        {
            return top;
        }

        internal int GetRightEdgePosition()
        {
            return left + width - 1;
        }

        internal int GetBottomEdgePosition()
        {
            return top + height - 1;
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
                    container.UpdateStructure();
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
                    container.UpdateStructure();
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
            foreach (Control control in this.containers)
                this.Update(control);
        }

        internal void AddContainer(Container container)
        {
            container.Buffer = this;
            this.containers.Add(container);
        }

        internal void RemoveControl(Control control)
        {
            if (this.PSHost != null)
            {
                foreach (BufferCellElement bce in this.bufferCellElements)
                {
                    if (bce.Control == control)
                        this.PSHost.UI.RawUI.SetBufferContents(bce.Coordinates, bce.CapturedBufferCellArray);
                }

                this.BufferedControls.Remove(control);
            }
        }

        public void Write()
        {
            this.UpdateAll();
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
        internal int width = 0;
        internal int height = 0;
        internal bool BorderTop { get; set; } = false;
        internal bool BorderRight { get; set; } = false;
        internal bool BorderBottom { get; set; } = false;
        internal bool BorderLeft { get; set; } = false;
        internal bool PaddingTop { get; set; } = false;
        internal bool PaddingRight { get; set; } = false;
        internal bool PaddingBottom { get; set; } = false;
        internal bool PaddingLeft { get; set; } = false;
        internal char BorderCharacter { get; set; } = '#';
        internal BufferCell BorderCell { get; set; } = new BufferCell(' ', 0, 0, 0);
        internal char PaddingCharacterTop { get; set; } = ' ';
        internal char PaddingCharacterRight { get; set; } = ' ';
        internal char PaddingCharacterBottom { get; set; } = ' ';
        internal char PaddingCharacterLeft { get; set; } = ' ';
        internal BufferCell PaddingCellTop { get; set; } = new BufferCell(' ', 0, 0, 0);
        internal BufferCell PaddingCellRight { get; set; } = new BufferCell(' ', 0, 0, 0);
        internal BufferCell PaddingCellBottom { get; set; } = new BufferCell(' ', 0, 0, 0);
        internal BufferCell PaddingCellLeft { get; set; } = new BufferCell(' ', 0, 0, 0);
        internal char BackgroundCharacter { get; set; } = ' ';
        internal BufferCell BackgroundCell { get; set; } = new BufferCell(' ', 0, 0, 0);
        internal char SelectCharacter { get; set; } = '+';
        internal char ActiveCharacter { get; set; } = '>';
        internal char SelectedAndActiveCharacter { get; set; } = '*';
        internal ConsoleColor BackgroundColor { get; set; }
        internal ConsoleColor ForegroundColor { get; set; }
        internal ConsoleColor ActiveItemColor { get; set; }
        internal ConsoleColor SelectedItemColor { get; set; }
        internal ConsoleColor ActiveAndSelectedItemColor { get; set; }
        internal string AlignText { get; set; } = "Left";

        // Controls
        internal const string KeyUp0  = "UpArrow";
        internal const string KeyUp1  = "K";
        internal const string KeyRight0  = "RightArrow";
        internal const string KeyRight1  = "L";
        internal const string KeyDown0  = "DownArrow";
        internal const string KeyDown1  = "J";
        internal const string KeyPageUp  = "PageUp";
        internal const string KeyPageDown  = "PageDown";
        internal const string KeyLeft0  = "LeftArrow";
        internal const string KeyLeft1  = "H";
        internal const string KeyConfirm = "Enter";
        internal const string KeySelect = "Spacebar";
        internal const string KeyCancel = "Escape";
        internal const string KeyFind = "Oem2";
        internal const string KeyFindNext = "N";
        internal const string KeyFindPrevious = "P";
        internal const string KeyTest = "T";

        // Returns a text representation of the control, including borders and whatever else stylings
        internal abstract List<string> GetTextRepresentation();

        // Returns a PSHost representation of the control
        internal abstract List<BufferCellElement> GetPSHostRawUIRepresentation();

        // A Container that contains this Control
        public Container Container { get; set; }

        public void UpdatePSHostVariables()
        {
            this.BackgroundColor = this.Container.Buffer.PSHost.UI.RawUI.ForegroundColor;
            this.ForegroundColor = this.Container.Buffer.PSHost.UI.RawUI.BackgroundColor;
            this.ActiveItemColor = ConsoleColor.Green;
            this.SelectedItemColor = ConsoleColor.Magenta;
            this.ActiveAndSelectedItemColor = ConsoleColor.Cyan;
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
            this.BackgroundCell = new BufferCell(this.BackgroundCharacter, 
                this.ForegroundColor, this.BackgroundColor, 0);
        }
        
        public void SetHorizontalPosition(int x)
        {
            if (this.Container != null)
            {
                if (x + this.GetWidth() - 1 > this.Container.GetRightEdgePosition())
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
                if (y + this.GetHeight() - 1 > this.Container.GetBottomEdgePosition())
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
            this.width = width;

            Buffer constrainer;
            var container = this as Container;

            if (container != null)
                constrainer = container.Buffer;
            else if (this.Container != null && this.Container.Buffer != null)
                constrainer = this.Container.Buffer;
            else
                // TODO Added this for Menu.TextRepresentation(), when it calls new Label without 
                // passing a buffer and then calls SetWidth. Either add this failsafe to SetHeight 
                // too or cook up some better fix.
                constrainer = new Buffer();

            if (width > constrainer.GetWidth())
                width = constrainer.GetWidth();

            if (this.GetLeftEdgePosition() > constrainer.GetLeftEdgePosition())
                this.SetLeftEdgePosition(constrainer.GetLeftEdgePosition());

            if (this.GetRightEdgePosition() > constrainer.GetRightEdgePosition())
                this.SetRightEdgePosition(constrainer.GetRightEdgePosition());
        }

        public int GetWidth()
        {
            return this.width;
        }

        public void SetHeight(int height)
        {
            this.height = height;

            Buffer constrainer;
            var container = this as Container;

            if (container != null)
                constrainer = container.Buffer;
            else
                constrainer = this.Container.Buffer;

            if (height > constrainer.GetHeight())
                height = constrainer.GetHeight();

            if (this.GetTopEdgePosition() > constrainer.GetTopEdgePosition())
                this.SetTopEdgePosition(constrainer.GetTopEdgePosition());

            if (this.GetBottomEdgePosition() > constrainer.GetBottomEdgePosition())
                this.SetBottomEdgePosition(constrainer.GetBottomEdgePosition());
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
            return this.Position.X + this.GetWidth() - 1;
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
            {
                this.SetWidth(x - this.GetLeftEdgePosition());
            }
        }

        public int GetBottomEdgePosition()
        {
            return this.Position.Y + this.GetHeight() - 1;
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

        internal Coordinates GetTopBorderCoordinates()
        {
            int left = this.GetLeftEdgePosition();
            int top = this.GetTopEdgePosition();
            return new Coordinates(left, top);
        }
        
        internal Coordinates GetBottomBorderCoordinates()
        {
            int left = this.GetLeftEdgePosition();
            int top = this.GetBottomEdgePosition();
            return new Coordinates(left, top);
        }

        internal int GetBorderHeight()
        {
            var height = this.GetHeight();
            if (this.BorderTop)
                height--;
            if (this.BorderBottom)
                height--;
            return height;
        }
        
        internal Coordinates GetLeftBorderCoordinates()
        {
            int left = this.GetLeftEdgePosition();
            var top = this.GetTopEdgePosition();
            if (this.BorderTop)
                top++;
            return new Coordinates(left, top);
        }

        internal Coordinates GetRightBorderCoordinates()
        {
            int left = this.GetRightEdgePosition();
            var top = this.GetTopEdgePosition();
            if (this.BorderTop)
                top++;
            return new Coordinates(left, top);
        }

        internal int GetPaddingWidth()
        {
            var width = this.GetWidth();
            if (this.BorderLeft)
                width--;
            if (this.BorderRight)
                width--;
            return width;
        }

        internal Coordinates GetTopPaddingCoordinates()
        {
            var left = this.GetLeftEdgePosition();
            if (this.BorderLeft)
                left++;
            var top = this.GetTopEdgePosition();
            if (this.BorderTop)
                top++;
            return new Coordinates(left, top);
        } 
        
        internal Coordinates GetBottomPaddingCoordinates()
        {
            var left = this.GetLeftEdgePosition();
            if (this.BorderLeft)
                left++;

            var top = this.GetBottomEdgePosition();
            if (this.BorderBottom)
                top--;

            return new Coordinates(left, top);
        }
        
        internal int GetPaddingHeight()
        {
            var height = this.GetHeight();
            if (this.BorderTop)
                height--;
            if (this.BorderBottom)
                height--;
            if (this.PaddingTop)
                height--;
            if (this.PaddingBottom)
                height--;
            return height;
        }

        internal Coordinates GetLeftPaddingCoordinates()
        {
            var left = this.GetLeftEdgePosition();
            if (this.BorderLeft)
                left++;

            var top = this.GetTopEdgePosition();
            if (this.PaddingTop)
                top++;
            if (this.BorderTop)
                top++;

            return new Coordinates(left, top);
        }

        internal Coordinates GetRightPaddingCoordinates()
        {
            var left = this.GetRightEdgePosition();
            if (this.BorderRight)
                left--;
            var top = this.GetTopEdgePosition();

            if (this.PaddingTop)
                top++;
            if (this.BorderTop)
                top++;

            return new Coordinates(left, top);
        }

        public int GetItemPositionTop(int itemNumber = 0)
        {
            var position = this.GetTopEdgePosition();
            if (this.PaddingTop)
                position++;
            if (this.BorderTop)
                position++;
            position += itemNumber;
            return position;
        }

        public int GetItemPositionBottom(int itemNumber = 0)
        {
            return this.GetItemPositionTop(itemNumber);
        }

        public int GetItemPositionLeft()
        {
            var position = this.GetLeftEdgePosition();
            if (this.PaddingLeft)
                position++;
            if (this.BorderLeft)
                position++;
            return position;
        }

        public int GetItemPositionRight()
        {
            var position = this.GetRightEdgePosition();
            if (this.PaddingRight)
                position--;
            if (this.BorderRight)
                position--;
            return position;
        }

        public int GetItemWidth()
        {
            var width = this.GetWidth();
            if (this.BorderLeft)
                width--;
            if (this.BorderRight)
                width--;
            if (this.PaddingLeft)
                width--;
            if (this.PaddingRight)
                width--;
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
            // TODO Maybe the position variables should be named to make it clearer that they're supposed 
            // to be used to form a rectangle that'll be used to capture the buffer before rewriting it.
            var positionTop = 0; 
            var positionBottom = 0;
            var positionLeft = 0;
            var positionRight = 0;
            var width = 1;
            var height = 1;
            var cell = this.BorderCell;

            if (element == "borderTop")
            {
                width = this.GetWidth();
                Coordinates coords = this.GetTopBorderCoordinates();
                positionTop = coords.Y;
                positionBottom = positionTop + 1;
                positionLeft = coords.X;
                positionRight = positionLeft + width;
                cell = this.BorderCell;
            }
            else if (element == "borderBottom")
            {
                width = this.GetWidth();
                Coordinates coords = this.GetBottomBorderCoordinates();
                positionTop = coords.Y;
                positionBottom = positionTop + 1;
                positionLeft = coords.X;
                positionRight = positionLeft + width;
                cell = this.BorderCell;
            }
            else if (element == "borderLeft")
            {
                height = this.GetBorderHeight();
                Coordinates coords = this.GetLeftBorderCoordinates();
                positionTop = coords.Y;
                positionBottom = positionTop + height;
                if (this.BorderBottom)
                    positionBottom--;
                positionLeft = coords.X;
                positionRight = positionLeft + 1;
                cell = this.BorderCell;
            }
            else if (element == "borderRight")
            {
                height = this.GetBorderHeight();
                Coordinates coords = this.GetRightBorderCoordinates();
                positionTop = coords.Y;
                positionBottom = positionTop + height;
                positionLeft = coords.X;
                positionRight = positionLeft + 1;
                cell = this.BorderCell;
            }
            else if (element == "paddingTop")
            {
                width = this.GetPaddingWidth();
                Coordinates coords = this.GetTopPaddingCoordinates();
                positionTop = coords.Y;
                positionBottom = positionTop + 1;
                positionLeft = coords.X;
                positionRight = positionLeft + width;
                if (this.BorderRight)
                    positionRight--;
                cell = this.PaddingCellTop;
            }
            else if (element == "paddingBottom")
            {
                width = this.GetPaddingWidth();
                Coordinates coords = this.GetBottomPaddingCoordinates();
                positionTop = coords.Y;
                positionBottom = positionTop + 1;
                positionLeft = coords.X;
                positionRight = positionLeft + width;
                if (this.BorderRight)
                    positionRight--;
                cell = this.PaddingCellBottom;
            }
            else if (element == "paddingLeft")
            {
                height = this.GetPaddingHeight();
                Coordinates coords = this.GetLeftPaddingCoordinates();
                positionTop = coords.Y;
                positionBottom = positionTop + height + 1; // TODO The + 1 doesn't seem to make sense but was required
                if (this.PaddingBottom)
                    positionBottom--;
                if (this.BorderBottom)
                    positionBottom--;
                positionLeft = coords.X;
                positionRight = positionLeft + 1;
                cell = this.PaddingCellLeft;
            }
            else if (element == "paddingRight")
            {
                height = this.GetPaddingHeight();
                Coordinates coords = this.GetRightPaddingCoordinates();
                positionTop = coords.Y;
                positionBottom = positionTop + height + 1; // TODO The + 1 doesn't seem to make sense but was required
                if (this.PaddingBottom)
                    positionBottom--;
                if (this.BorderBottom)
                    positionBottom--;
                positionLeft = coords.X;
                positionRight = positionLeft + 1;
                cell = this.PaddingCellRight;
            }
            else if (element.ToLower().Contains("item"))
            {
                positionTop = this.GetItemPositionTop(itemNumber);
                positionBottom = this.GetItemPositionBottom(itemNumber) + 1;
                positionLeft = this.GetItemPositionLeft();
                positionRight = this.GetItemPositionRight();
                width = this.GetItemWidth();
                cell = this.PaddingCellBottom;
            }

            var coordinates = new Coordinates(positionLeft, positionTop); // x, y
            var rectangle = new Rectangle(
                positionLeft, positionTop, positionRight, positionBottom); // left, top, right, bottom
            var bufferContent = this.Container.Buffer.PSHost.UI.RawUI.GetBufferContents(rectangle);
            var size = new Size(width, height);
            var bufferContentNew = this.Container.Buffer.PSHost.UI.RawUI.NewBufferCellArray(size, cell);
            var bufferCellElement = new BufferCellElement(bufferContent, bufferContentNew, 
                                                            coordinates, control);
            if (element == "item")
            {
                bufferContentNew = this.Container.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), this.ForegroundColor, this.BackgroundColor);
                bufferCellElement = new BufferCellElement(
                    bufferContent, bufferContentNew, coordinates, control, obj);
            }
            else if (element == "activeItem")
            {
                bufferContentNew = this.Container.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), this.ActiveItemColor, this.BackgroundColor);
                bufferCellElement = new BufferCellElement(
                    bufferContent, bufferContentNew, coordinates, control, obj);
            }
            else if (element == "selectedItem")
            {
                bufferContentNew = this.Container.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), this.SelectedItemColor, this.BackgroundColor);
                bufferCellElement = new BufferCellElement(
                    bufferContent, bufferContentNew, coordinates, control, obj);
            }
            else if (element == "activeAndSelectedItem")
            {
                bufferContentNew = this.Container.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), this.ActiveAndSelectedItemColor, this.BackgroundColor);
                bufferCellElement = new BufferCellElement(
                    bufferContent, bufferContentNew, coordinates, control, obj);
            }
            
            return bufferCellElement;
        }

        internal List<BufferCellElement> GetPSHostRawUIBorderRepresentation()
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
            var textHorizontalSpace = this.GetItemHorizontalSpace();
            var bce = this.Container.Buffer.GetBufferCellElement(findItem);
            if (bce != null)
            {
                List<string> text = new List<string>();
                var outTxt = findItem.ToString();
                int leftFillCount = 0; // Number of fill characters on left side of text
                if (this.AlignText == "center" && outTxt.Length < textHorizontalSpace)
                {
                    leftFillCount = (textHorizontalSpace - outTxt.Length) / 2;
                    outTxt = new String(this.BackgroundCharacter, leftFillCount) + outTxt;
                }
                outTxt = outTxt.PadRight(this.GetItemHorizontalSpace(), this.BackgroundCharacter);
                text.Add(outTxt);
                bce.NewBufferCellArray = this.Container.Buffer.PSHost.UI.RawUI.NewBufferCellArray(
                    text.ToArray(), foregroundColor, backgroundColor);
                bce.Changed = true;
            }
        }
    }

    class Container : Control
    {
        internal Buffer Buffer { get; set; }
        internal bool Vertical { get; set; } = false;
        internal bool SetContainerToWidestControlWidth { get; set; } = true;
        internal bool SetControlsToContainerWidth { get; set; } = true;
        internal bool AutoPositionControls { get; set; } = true;
        internal bool AutoPositionContainer { get; set; } = true;
        internal bool SetContainerToCombinedControlHeight { get; set; } = true;

        internal List<Control> controls = new List<Control>();

        internal Container(Buffer buffer)
        {
            this.Buffer = buffer;
            this.Buffer.AddContainer(this);
            this.SetHorizontalPosition(this.Buffer.GetLeftEdgePosition());
            this.SetVerticalPosition(this.Buffer.GetTopEdgePosition());
            this.SetWidth(this.Buffer.GetWidth());
            this.SetHeight(this.Buffer.GetHeight());
        }

        internal Container(int left, int top, int width, int height)
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(width);
            this.SetHeight(height);
        }

        internal void SetControlsWidth(int width)
        {
            foreach (Control control in this.controls)
                control.SetWidth(width);
        }

        internal int GetWidestControlWidth()
        {
            int i = 0;
            foreach (Control control in this.controls)
            {
                int y = control.GetWidth();
                if (y > i)
                    i = y;
            }
            return i;
        }

        internal void AddControl(Control control)
        {
            control.Container = this;
            this.controls.Add(control);
        }

        internal void UpdateStructure()
        {
            // Updates Container and Control positions and dimensions

            void AutoPositionControl(Control control, int itemIndex)
            {
                if (this.AutoPositionControls && itemIndex == 0)
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
                        var lastControl = controls[itemIndex - 1];
                        var left = this.Position.X;
                        var top = lastControl.GetBottomEdgePosition() + 1;
                        control.SetHorizontalPosition(left);
                        control.SetVerticalPosition(top);
                    }
                    else
                        throw new NotImplementedException();
                }
            }

            if (this.SetContainerToCombinedControlHeight)
                this.SetHeight(this.Buffer.GetHeight());

            if (this.SetContainerToWidestControlWidth)
                this.SetWidth(0);

            int index = 0;

            foreach (Control control in this.controls)
            {
                if (this.SetContainerToWidestControlWidth)
                {
                    if (this.GetWidth() < control.GetWidth())
                        this.SetWidth(control.GetWidth());
                }

                AutoPositionControl(control, index);

                if (control.GetBottomEdgePosition() > this.GetBottomEdgePosition())
                    control.SetBottomEdgePosition(this.GetBottomEdgePosition());

                index++;
            }

            if (this.SetControlsToContainerWidth)
            {
                foreach (Control control in this.controls)
                    control.SetWidth(this.GetWidth());
            }

            if (this.SetContainerToCombinedControlHeight)
            {
                var height = 0;

                foreach (Control control in this.controls)
                    height += control.GetHeight();

                this.SetHeight(height);
            }

            if (this.AutoPositionContainer)
            {
                this.SetHorizontalPositionToMiddle();
                this.SetVerticalPositionToMiddle();

                index = 0;

                foreach (Control control in this.controls)
                {
                    AutoPositionControl(control, index);
                    index++;
                }
            }
        }

        internal void RemoveControl(Control control)
        {
            if (this.controls.Contains(control))
            {
                this.Buffer.RemoveControl(control);
                this.controls.Remove(control);
            }
        }

        internal void RemoveAllControls()
        {
            while (this.controls.Count > 0)
                this.RemoveControl(this.controls[0]);
        }

        internal void Close()
        {
            this.RemoveAllControls();
            this.Buffer = null;
        }

        public void SetHorizontalPositionToMiddle()
        {
            var x = this.Buffer.GetWidth() / 2 - this.GetWidth() / 2;
            this.SetHorizontalPosition(x);
        }

        public void SetVerticalPositionToMiddle()
        {
            var y = this.Buffer.GetHeight() / 2 - this.GetHeight() / 2;
            this.SetVerticalPosition(y);
        }

        internal override List<string> GetTextRepresentation()
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

        internal override List<BufferCellElement> GetPSHostRawUIRepresentation()
        {
            throw new NotImplementedException();
        }
    }

    class Label : Control
    {
        internal List<string> Text { get; set; } = new List<string>();

        internal Label(int left, int top, string text) : base()
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.width = text.Length;
            this.height = 1;
            this.Text.Add(text);
        }

        internal Label(Container container, int left, int top, List<string> text) : base()
        {
            this.Container = container;
            this.Container.AddControl(this);
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            
            foreach (string txt in text)
            {
                if (this.GetWidth() < txt.Length)
                    this.width = txt.Length;
            }

            this.height = text.Count;
            this.Text = text;
        }
        
        internal override List<string> GetTextRepresentation()
        {
            var outText = new List<string>();
            var text = this.Text;
            var horizontalBorder = new string(this.BorderCharacter, this.GetWidth());
            var count = this.GetWidth() - 2;
            if (count < 0)
                count = 0;
            var emptyLine = new String(this.BackgroundCharacter, count);
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
                    outTxt = new String(this.BackgroundCharacter, leftFillCount) + outTxt;
                }

                if (outTxt.Length > textHorizontalSpace)
                    outTxt = outTxt.Substring(0, textHorizontalSpace);
                else
                    outTxt = outTxt.PadRight(textHorizontalSpace, this.BackgroundCharacter);

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
                        emptyLine = emptyLine + this.BackgroundCharacter;
                    
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
                        emptyLine = this.BackgroundCharacter + emptyLine;

                    if (!this.BorderRight && !this.BorderLeft && this.GetWidth() == 1)
                        emptyLine = "" + this.BackgroundCharacter;
                }

                if (this.PaddingTop)
                    outText.Add(emptyLine.Replace(this.BackgroundCharacter, this.PaddingCharacterTop));

                outText.Add(outTxt);
            }

            var numberOfEmptyLines = this.GetHeight();
            numberOfEmptyLines -= text.Count;

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
                for (var i = 1; i <= numberOfEmptyLines; i++)
                {
                    outText.Add(emptyLine);
                }
            }

            if (this.PaddingBottom)
                outText.Add(emptyLine.Replace(this.BackgroundCharacter, this.PaddingCharacterBottom));
            
            if (this.BorderBottom)
                outText.Add(horizontalBorder);
            
            return outText;
        }
        
        internal override List<BufferCellElement> GetPSHostRawUIRepresentation()
        {
            var bufferCellElement = new List<BufferCellElement>();

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
                    outTxt = new String(this.BackgroundCharacter, leftFillCount) + outTxt;
                }

                outTxt = outTxt.PadRight(textHorizontalSpace, this.BackgroundCharacter);
                content.Add(outTxt);
            }

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
            
            bufferCellElement.Add(NewBufferCellElement("item", content, control: this));

            if (GetNumberOfAvailableContentRows() > content.Count)
            {
                string spaces = new String(this.BackgroundCharacter, textHorizontalSpace);

                for (int i = 1; i < GetNumberOfAvailableContentRows(); i++)
                    content.Add(spaces);

                bufferCellElement.Add(NewBufferCellElement("item", content, control: this));
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

        public TextBox(Container container, int left, int top, int width) : base()
        {
            this.Container = container;
            this.Container.AddControl(this);
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(width);
            this.SetHeight(1);
            this.Text = " ";
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
                        Console.Write(this.BackgroundCharacter);
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

        internal override List<string> GetTextRepresentation()
        {
            var text = new List<string>();
            var txt = this.Text;
            var horizontalBorder = new string(this.BorderCharacter, this.GetWidth());
            var count = this.GetWidth() - 2;
            if (count < 0)
                count = 0;
            var emptyLine = new String(this.BackgroundCharacter, count);
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
                txt = txt.PadRight(textHorizontalSpace, this.BackgroundCharacter);

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
                    emptyLine = emptyLine + this.BackgroundCharacter;
                
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
                    emptyLine = this.BackgroundCharacter + emptyLine;

                if (!this.BorderRight && !this.BorderLeft && this.GetWidth() == 1)
                    emptyLine = "" + this.BackgroundCharacter;
            }

            if (this.PaddingTop)
                text.Add(emptyLine.Replace(this.BackgroundCharacter, this.PaddingCharacterTop));

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
                text.Add(emptyLine.Replace(this.BackgroundCharacter, this.PaddingCharacterBottom));
            
            if (this.BorderBottom)
                text.Add(horizontalBorder);

            return text;
        }
        
        internal override List<BufferCellElement> GetPSHostRawUIRepresentation()
        {
            var bufferCellElement = new List<BufferCellElement>();

            UpdatePSHostVariables();

            var textHorizontalSpace = this.GetItemHorizontalSpace();
            List<string> content = new List<string>();
            string txt = this.Text;
            if (txt.Length == 0)
                txt = " ";
            txt = txt.PadRight(textHorizontalSpace, this.BackgroundCharacter);
            content.Add(txt);

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
                bufferCellElement.Add(NewBufferCellElement("item", content, control: this));
            else if (GetNumberOfAvailableContentRows() > 1)
            {
                string spaces = new String(this.BackgroundCharacter, this.Text.Length);
                for (int i = 1; i < GetNumberOfAvailableContentRows(); i++)
                    content.Add(spaces);
                bufferCellElement.Add(NewBufferCellElement("item", content, control: this));
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

        public Menu(Container container, int left, int top, List<Object> objects) : base()
        {
            this.Container = container;
            this.Container.AddControl(this);
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);

            for (var i = 0; i < objects.Count; i++)
            {
                var objLength = objects[i].ToString().Length;
                if (objLength > this.GetWidth())
                    this.width = objLength;
            }

            this.height = objects.Count;
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
            void Update()
            {
                this.Container.Buffer.Write();
            }

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
                        Update();
                        break;
                    case KeyDown0:
                    case KeyDown1:
                        if (this.Mode == "List")
                            break;
                        this.SetNextItemActive();
                        Update();
                        break;
                    case KeyPageUp:
                        // TODO There's more of a flash effect on PSHost than there probably 
                        // needs to as in it's likely restoring the saved buffer area before 
                        // writing the new content when it doesn't need to restore
                        this.LoadPreviousPage();
                        Update();
                        break;
                    case KeyPageDown:
                        // TODO There's more of a flash effect on PSHost than there probably 
                        // needs to as in it's likely restoring the saved buffer area before 
                        // writing the new content when it doesn't need to restore
                        this.LoadNextPage();
                        Update();
                        break;
                    case KeyFind:
                        if (this.Mode == "List")
                            break;
                        
                        Buffer sbBuffer = new Buffer();
                        if (this.Container.Buffer.Type == "Console")
                            sbBuffer = new Buffer();
                        else
                            sbBuffer = new Buffer(this.Container.Buffer.PSHost);
                        
                        Container sbContainer = new Container(sbBuffer);

                        int left = this.Container.GetLeftEdgePosition();
                        int top = this.Container.GetTopEdgePosition() + (this.Container.GetHeight() / 2);
                        int width = this.Container.GetWidth();

                        var searchBox = new TextBox(sbContainer, left, top, width);
                        searchBox.AddBorder("all");
                        searchBox.AddPadding("all");

                        sbBuffer.Write();
                        searchTerm = searchBox.ReadKey();
                        sbContainer.RemoveAllControls();

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
                        {
                            this.SetItemActive(this.Objects.IndexOf(item));
                        
                            if (this.Objects.Count > this.GetNumberOfAvailableRowsForItems() &&
                                !this.DisplayedObjects.Contains(item))
                                this.MoveActiveObjectToMiddle();
                        }
                        
                        Update();
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
                        Update();
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
                        Update();
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
                        Update();
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
                newForegroundColor = this.SelectedItemColor;
            else
                newForegroundColor = this.ForegroundColor;

            if (this.Container.Buffer != null && this.Container.Buffer.PSHost != null)
                this.SetObjectBufferCellElement(this.ActiveObject, newForegroundColor, this.BackgroundColor);

            this.ActiveObject = this.Objects[itemIndex];

            if (this.SelectedObjects.Contains(this.ActiveObject))
                newForegroundColor = this.ActiveAndSelectedItemColor;
            else
                newForegroundColor = this.ActiveItemColor;

            if (this.Container.Buffer != null && this.Container.Buffer.PSHost != null)
                this.SetObjectBufferCellElement(this.ActiveObject, newForegroundColor, this.BackgroundColor);
        }

        private void AddSelectedObject(Object item)
        {
            ConsoleColor newForegroundColor;

            if (!this.SelectedObjects.Contains(item))
                this.SelectedObjects.Add(item);

            if (this.ActiveObject == item)
                newForegroundColor = this.ActiveAndSelectedItemColor;
            else
                newForegroundColor = this.SelectedItemColor;

            if (this.Container.Buffer != null && this.Container.Buffer.PSHost != null)
                this.SetObjectBufferCellElement(this.ActiveObject, newForegroundColor, this.BackgroundColor);
        }

        private void RemoveSelectedObject(Object item)
        {
            ConsoleColor newForegroundColor;

            if (this.SelectedObjects.Contains(item))
                this.SelectedObjects.Remove(item);

            if (this.ActiveObject == item)
                newForegroundColor = this.ActiveItemColor;
            else
                newForegroundColor = this.ForegroundColor;

            if (this.Container.Buffer != null && this.Container.Buffer.PSHost != null)
                this.SetObjectBufferCellElement(this.ActiveObject, newForegroundColor, this.BackgroundColor);
        }

        private void LoadNextPage()
        {
            this.TopDisplayedObjectIndex = this.BottomDisplayedObjectIndex + 1;
            if (this.TopDisplayedObjectIndex == this.Objects.Count)
                this.TopDisplayedObjectIndex = 0;

            this.Container.Buffer.Clear();

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

            this.Container.Buffer.Clear();

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

            this.Container.Buffer.Clear();
        }

        internal override List<string> GetTextRepresentation()
        {
            var text = new List<string>();
            this.DisplayedObjects = new List<Object>();

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
                if (currentItemIndex > this.Objects.Count - 1)
                    currentItemIndex = 0;

                var item = this.Objects[currentItemIndex];
                var outTxt = item.ToString();
                int leftFillCount = 0; // Number of fill characters on left side of text

                if (this.AlignText == "center" && outTxt.Length < textHorizontalSpace)
                {
                    leftFillCount = (textHorizontalSpace - outTxt.Length) / 2;
                    outTxt = new String(this.BackgroundCharacter, leftFillCount) + outTxt;
                }

                this.BottomDisplayedObjectIndex = currentItemIndex;
                this.DisplayedObjects.Add(item);

                if (!this.PaddingLeft)
                {
                    if (this.ActiveObject == item && this.SelectedObjects.Contains(item) &&
                        this.Mode != "List")
                        outTxt = "" + this.SelectedAndActiveCharacter + this.BackgroundCharacter + outTxt;
                    else if (this.ActiveObject == item && this.Mode != "List")
                        outTxt = "" + this.ActiveCharacter + this.BackgroundCharacter + outTxt;
                    else if (this.SelectedObjects.Contains(item))
                        outTxt = "" + this.SelectCharacter + this.BackgroundCharacter + outTxt;
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

        internal override List<BufferCellElement> GetPSHostRawUIRepresentation()
        {
            var bufferCellElement = new List<BufferCellElement>();
            UpdatePSHostVariables();
            this.DisplayedObjects = new List<Object>();
            var textHorizontalSpace = this.GetItemHorizontalSpace();

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
                    outTxt = new String(this.BackgroundCharacter, leftFillCount) + outTxt;
                }

                outTxt = outTxt.PadRight(textHorizontalSpace, this.BackgroundCharacter);
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