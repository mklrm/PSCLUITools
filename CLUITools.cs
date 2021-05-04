using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PSCLUITools
{
    abstract class Buffer : PSCmdlet
    {
        internal static int left = Console.WindowLeft;
        internal static int top = Console.WindowTop;
        internal static Coordinates position = new Coordinates(left, top);
        internal static int width = Console.WindowWidth;
        internal static int height = Console.WindowHeight;
        protected Container container = new Container(left, top, width, height);

        // Inserts 'List<string> text' on the buffer starting from row and column
        public abstract void Insert(int row, int column, List<string> text);

        // Inserts attached controls to the buffer
        public abstract void Update(Control control);

        // Inserts all attached controls to the buffer
        public abstract void UpdateAll();

        // Writes the buffer to the console
        public abstract void Write();
        
        // Clears the console buffer area or restores the original state
        public abstract void Clear();

        public void AddControl(Control control)
        {
            control.Buffer = this;
            this.container.controls.Add(control);
        }

        public void RemoveControl(Container container)
        {
            throw new NotImplementedException();
        }
    }

    class ConsoleBuffer : Buffer
    {
        // Used where PSHost is not available (such as Linux)
        // Heavily loans from: http://cgp.wikidot.com/consle-screen-buffer

        private static char[,] screenBufferArray = new char[width,height];

        public ConsoleBuffer()
        {
            this.container.SetContainerToWidestControlWidth = false;
            this.container.SetControlsToContainerWidth = false;
            this.container.AutoPositionControls = false;
        }

        public override void Insert(int column, int row, List<string> text)
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

        public override void Update(Control control)
        {
            if (control is Container)
            {
                var container = (Container) control;
                foreach (Control childControl in container.controls)
                {
                    this.Update(childControl);
                }
            }
            else
            {
                var left = control.Position.X;
                var top = control.Position.Y;
                var text = control.ToTextRepresentation();
                this.Insert(left, top, text);
            }
        }

        public override void UpdateAll()
        {
            foreach (Control control in this.container.controls)
            {
                this.Update(control);
            }
        }

        public override void Write()
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

        public override void Clear()
        {
            throw new NotImplementedException();
        }
    }

    class PSHostBuffer : Buffer
    {
        public override void Insert(int row, int column, List<string> text)
        {
            throw new NotImplementedException();
        }

        public override void Update(Control control)
        {
            throw new NotImplementedException();
        }

        public override void UpdateAll()
        {
            throw new NotImplementedException();
        }

        public override void Write()
        {
            throw new NotImplementedException();
        }

        public override void Clear()
        {
            throw new NotImplementedException();
        }
    }

    abstract class Control : PSCmdlet
    {
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
        public char PaddingCharacterTop { get; set; } = '.';
        public char PaddingCharacterRight { get; set; } = '.';
        public char PaddingCharacterBottom { get; set; } = '.';
        public char PaddingCharacterLeft { get; set; } = '.';
        public char FillCharacter { get; set; } = '`';
        public char SelectCharacter { get; set; } = '+';
        public char ActiveCharacter { get; set; } = '>';
        public char SelectedAndActiveCharacter { get; set; } = '*';
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

        // Returns a text representation of the control, including borders and whatever else stylings
        public abstract List<string> ToTextRepresentation();

        // Returns a layered representation of the control
        public abstract List<Object> ToLayerRepresentation();
        // TODO Layered isn't actually the right approach/name/description as I probably will have to be 
        //      returning a bunch of rectangles, single strings and lists of strings each including 
        //      coordinates that PSHostBuffer will then place on the console.

        // A Container that contains this Control
        public Container Container { get; set; }

        public Buffer Buffer { get; set; }
        // TODO Respect boundaries of other Controls within the Container

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
                    Console.WriteLine(this.GetWidth());
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

            if (control is Menu)
                ((Menu) control).SetMiddleMenuItemActive(); // TODO Find out how to cast as Menu at 
                                                            //      the beginning of the method

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

        public override List<string> ToTextRepresentation()
        {
            var text = new List<string>();

            foreach (Control control in this.controls)
            {
                foreach (string txt in control.ToTextRepresentation())
                {
                    text.Add(txt);
                }
            }

            return text;
        }

        public override List<Object> ToLayerRepresentation()
        {
            throw new NotImplementedException();
        }
    }

    class Label : Control
    {
        public string Text { get; set; }

        public Label(int left, int top, string text)
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(text.Length);
            this.SetHeight(1);
            this.Text = text;
        }

        public Label(int left, int top, int width, int height)
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetWidth(width);
            this.SetHeight(height);
        }

        public override List<string> ToTextRepresentation()
        {
            var text = new List<string>();
            var txt = this.Text;
            var horizontalBorder = new string(this.BorderCharacter, this.GetWidth());
            var count = this.GetWidth() - 2;
            if (count < 0)
                count = 0;
            var emptyLine = new String(this.FillCharacter, count);
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
        
        public override List<Object> ToLayerRepresentation()
        {
            throw new NotImplementedException();
        }
    }

    class Menu : Control
    {
        public List<Object> Objects { get; set; } = new List<Object>();
        public int TopDisplayedObjectIndex { get; set; } = 0; // Index of the object at the top of the list
        public List<Object> SelectedObjects { get; set; } = new List<Object>(); // Selected objects
        public Object ActiveObject { get; set; } = 0; // Highlighted object

        public Menu(int left, int top, List<Object> objects)
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
            this.ActiveObject = objects[0];
        }

        private int GetNumberOfAvailebleRowsForItems()
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
            var findItem = this.ActiveObject;
            while (true)
            {
                var key = Console.ReadKey(true).Key; // true hides key strokes
                switch (key.ToString())
                {
                    case KeyUp0:
                    case KeyUp1:
                        this.SetPreviousItemActive();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyDown0:
                    case KeyDown1:
                        this.SetNextItemActive();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyPageUp:
                        SetPreviousPage();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyPageDown:
                        SetNextPage();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyFind:
                        // TODO Add a Input control that takes... input. So it can be 
                        //      dropped over on a position that makes sense and be readable.
                        Console.SetCursorPosition(0,Console.WindowHeight);
                        Console.Write("Find: ");
                        ConsoleKeyInfo searchKey = Console.ReadKey(true);
                        searchTerm = "";
                        while (searchKey.Key != ConsoleKey.Enter)
                        {
                            searchTerm += searchKey.KeyChar;
                            Console.Write(searchKey.KeyChar);
                            searchKey = Console.ReadKey(true);
                        }
                        findItem = this.FindNextItem(searchTerm);
                        if (findItem != null)
                        {
                            this.ActiveObject = findItem;
                        }
                        if (this.Objects.Count > this.GetNumberOfAvailebleRowsForItems())
                            this.MoveActiveObjectToMiddle();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyFindNext:
                        findItem = this.FindNextItem(searchTerm);
                        if (findItem != null)
                        {
                            this.ActiveObject = findItem;
                        }
                        if (this.Objects.Count > this.GetNumberOfAvailebleRowsForItems())
                            this.MoveActiveObjectToMiddle();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyFindPrevious:
                        findItem = this.FindPreviousItem(searchTerm);
                        if (findItem != null)
                        {
                            this.ActiveObject = findItem;
                        }
                        if (this.Objects.Count > this.GetNumberOfAvailebleRowsForItems())
                            this.MoveActiveObjectToMiddle();
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeySelect:
                        if (this.SelectedObjects.Contains(this.ActiveObject))
                            this.SelectedObjects.Remove(this.ActiveObject);
                        else
                            this.SelectedObjects.Add(this.ActiveObject);
                        // TODO If this.Mode == "Default" or something just return
                        this.Buffer.UpdateAll();
                        this.Buffer.Write();
                        break;
                    case KeyConfirm:
                        return this.SelectedObjects;
                    case KeyCancel:
                        return new List<Object>();
                }
            }
        }

        public void SetNextItemActive()
        {
            var activeObjectIndex = this.Objects.IndexOf(this.ActiveObject);

            if (this.Objects.Count > this.GetNumberOfAvailebleRowsForItems())
            {
                if (activeObjectIndex + 1 < this.Objects.Count)
                {
                    this.ActiveObject = this.Objects[activeObjectIndex + 1];
                    this.TopDisplayedObjectIndex++;
                }
                else
                {
                    this.ActiveObject = this.Objects[0];
                    this.TopDisplayedObjectIndex++;
                }

                if (this.TopDisplayedObjectIndex >= this.Objects.Count)
                    this.TopDisplayedObjectIndex = 0;
            }
            else
            {
                if (activeObjectIndex + 1 < this.Objects.Count)
                    this.ActiveObject = this.Objects[activeObjectIndex + 1];
                else
                    this.ActiveObject = this.Objects[0];
            }
        }

        public void SetPreviousItemActive()
        {
            var activeObjectIndex = this.Objects.IndexOf(this.ActiveObject);

            if (this.Objects.Count > this.GetNumberOfAvailebleRowsForItems())
            {
                if (activeObjectIndex - 1 >= 0)
                {
                    this.ActiveObject = this.Objects[activeObjectIndex - 1];
                    this.TopDisplayedObjectIndex--;
                }
                else
                {
                    this.ActiveObject = this.Objects[this.Objects.Count - 1];
                    this.TopDisplayedObjectIndex--;
                }

                if (this.TopDisplayedObjectIndex < 0)
                    this.TopDisplayedObjectIndex = this.Objects.Count - 1;
            }
            else
            {
                if (activeObjectIndex - 1 >= 0)
                    this.ActiveObject = this.Objects[activeObjectIndex - 1];
                else
                    this.ActiveObject = this.Objects[this.Objects.Count - 1];
            }
        }

        public Object FindNextItem(string searchTerm)
        {
            searchTerm = searchTerm.ToLower();
            Regex regex = new Regex(searchTerm);
            var index = this.Objects.IndexOf(this.ActiveObject);
            var firstIndex = index;
            while (true)
            {
                index++;
                if (index > this.Objects.Count - 1)
                {
                    index = 0;
                }

                if (index == firstIndex)
                {
                    return null;
                }

                var control = this.Objects[index];
                Match match = regex.Match(control.ToString().ToLower());
                if (match.Success)
                {
                    return control;
                }
            }
        }

        public Object FindPreviousItem(string searchTerm)
        {
            searchTerm = searchTerm.ToLower();
            Regex regex = new Regex(searchTerm);
            var index = this.Objects.IndexOf(this.ActiveObject);
            var firstIndex = index;
            while (true)
            {
                index--;
                if (index < 0)
                {
                    index = this.Objects.Count - 1;
                }

                if (index == firstIndex)
                {
                    return null;
                }

                var control = this.Objects[index];
                Match match = regex.Match(control.ToString().ToLower());
                if (match.Success)
                {
                    return control;
                }
            }
        }

        public void SetMiddleMenuItemActive()
        {
            var middleRowNumber = this.GetNumberOfAvailebleRowsForItems() / 2;
            if (middleRowNumber >= 0 && middleRowNumber <= this.GetBottomEdgePosition()
                && (middleRowNumber <= this.Objects.Count - 1))
                this.TopDisplayedObjectIndex = this.Objects.Count - middleRowNumber;
        }

        public void SetNextPage()
        {
            var rows = this.GetNumberOfAvailebleRowsForItems();
            if (this.Objects.Count > rows)
            {
                rows = rows / 2;
                for (var i = 1; i <= rows; i++)
                {
                    SetNextItemActive();
                }
            }
        }

        public void MoveActiveObjectToMiddle()
        {
            var newTopObjectIndex = this.Objects.IndexOf(this.ActiveObject);
            newTopObjectIndex = newTopObjectIndex - (GetNumberOfAvailebleRowsForItems() / 2);
            if (newTopObjectIndex < 0)
            {
                newTopObjectIndex = this.Objects.Count - 1 - Math.Abs(newTopObjectIndex);
            }
            this.TopDisplayedObjectIndex = newTopObjectIndex;
        }

        public void SetPreviousPage()
        {
            var rows = this.GetNumberOfAvailebleRowsForItems();
            if (this.Objects.Count > rows)
            {
                rows = rows / 2 + 1;
                for (var i = 1; i <= rows; i++)
                {
                    SetPreviousItemActive();
                }
            }
        }

        public new void SetHeight(int height)
        {
            base.SetHeight(height);
            SetMiddleMenuItemActive();
        }

        public new void AddBorder(string edge)
        {
            base.AddBorder(edge);
            SetMiddleMenuItemActive();
        }

        public new void RemoveBorder(string edge)
        {
            base.RemoveBorder(edge);
            SetMiddleMenuItemActive();
        }

        public new void SetTopEdgePosition(int y)
        {
            base.SetTopEdgePosition(y);
            SetMiddleMenuItemActive();
        }

        public new void SetBottomEdgePosition(int y)
        {
            base.SetBottomEdgePosition(y);
            SetMiddleMenuItemActive();
        }

        public override List<string> ToTextRepresentation()
        {
            var text = new List<string>();
            if (this.GetHeight() == 0)
                return text;
            var horizontalBorder = new string(this.BorderCharacter, this.GetWidth());
            if ((this.BorderTop || this.BorderBottom) && this.GetHeight() == 1)
            {
                text.Add(horizontalBorder);
                return text;
            }
            var rowsAvailableForItems = this.GetNumberOfAvailebleRowsForItems();
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
                var txt = item.ToString();

                if (!this.PaddingLeft)
                {
                    if (this.ActiveObject == item && this.SelectedObjects.Contains(item))
                        txt = "" + this.SelectedAndActiveCharacter + this.FillCharacter + txt;
                    else if (this.ActiveObject == item)
                        txt = "" + this.ActiveCharacter + this.FillCharacter + txt;
                    else if (this.SelectedObjects.Contains(item))
                        txt = "" + this.SelectCharacter + this.FillCharacter + txt;
                }

                var label = new Label(0, 0, txt);
                var width = this.GetWidth();

                if (this.PaddingLeft)
                {
                    if (this.ActiveObject == item && this.SelectedObjects.Contains(item))
                        label.PaddingCharacterLeft = this.SelectedAndActiveCharacter;
                    else if (this.ActiveObject == item)
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

                foreach (string lblText in label.ToTextRepresentation())
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

        public override List<Object> ToLayerRepresentation()
        {
            throw new NotImplementedException();
        }
    }
}