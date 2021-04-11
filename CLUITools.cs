using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Collections.Generic;

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
        public char BorderCharacter { get; set; } = '#';
        public char FillCharacter { get; set; } = '.';
        public char SelectCharacter { get; set; } = '>';

        // Returns a text representation of the control, including borders and whatever else stylings
        public abstract List<string> ToTextRepresentation();

        // Returns a layered representation of the control
        public abstract List<Object> ToLayerRepresentation();
        // TODO Layered isn't actually the right approach/name/description as I probably will have to be 
        //      returning a bunch of rectangles, single strings and lists of strings each including 
        //      coordinates that PSHostBuffer will then place on the console.

        // A Container that contains this Control
        public Container Container { get; set; }

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

            if (this.SetContainerToWidestControlWidth)
            {
                if (this.GetWidth() < control.GetWidth())
                {
                    this.SetWidth(control.GetWidth());

                    if (this.SetControlsToContainerWidth)
                        // Set existing member controls to the changed width
                        SetControlsWidth(this.GetWidth());
                }
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
            {
                control.SetBottomEdgePosition(this.GetBottomEdgePosition());
            }
            // TODO control.Container becomes inaccessible unless I make it public, this is not good.
            //      There's an explanation over yonder:
            //      https://stackoverflow.com/questions/567705/why-cant-i-access-c-sharp-protected-members-except-like-this
            controls.Add(control);
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
                {
                    // And it will be a border one
                    text.Add(this.BorderCharacter.ToString());
                    return text;
                }
                else
                {
                    text.Add(txt.Substring(0,1));
                    return text;
                }
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
                if (this.BorderRight)
                {
                    txt = txt + this.BorderCharacter;
                    emptyLine = emptyLine + this.BorderCharacter;
                }
                else
                    emptyLine = emptyLine + this.FillCharacter;
                
                if (this.BorderLeft)
                {
                    txt = this.BorderCharacter + txt;
                    emptyLine = this.BorderCharacter + emptyLine;
                }
                else
                    emptyLine = this.FillCharacter + emptyLine;

                if (!this.BorderRight && !this.BorderLeft && this.GetWidth() == 1)
                    emptyLine = "" + this.FillCharacter;
            }

            text.Add(txt);

            var numberOfEmptyLines = this.GetHeight();

            if (this.BorderTop)
                numberOfEmptyLines--;

            if (this.BorderBottom)
                numberOfEmptyLines--;

            if (numberOfEmptyLines > 1)
            {
                for (var i = 1; i < numberOfEmptyLines; i++)
                {
                    text.Add(emptyLine);
                }
            }

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

        // TODO Start adding methods for controlling the list of items

        public Menu(int left, int top, List<Object> objects)
        {
            this.SetHorizontalPosition(left);
            this.SetVerticalPosition(top);
            this.SetHeight(objects.Count);
            this.Objects = objects;
        }

        private int GetNumberOfAvailebleRowsForItems()
        {
            // Returns the number of rows that can fit items on the menu
            var rows = this.GetHeight();
            if (this.BorderTop)
                rows--;
            if (this.BorderBottom)
                rows--;
            if (rows < 0)
                rows = 0;
            return rows;
        }

        public List<Object> ReadKey()
        {
            while (true)
            {
                var key = Console.ReadKey(true).Key; // true hides key strokes
                switch (key)
                {
                    case ConsoleKey.Enter:
                        return this.SelectedObjects;
                    case ConsoleKey.Escape:
                        return new List<Object>();
                }
            }
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

            for (var i = 0; i < rowsAvailableForItems; i++)
            {
                if (currentItemIndex > Objects.Count - 1)
                    currentItemIndex = 0;

                var item = this.Objects[currentItemIndex];
                var txt = item.ToString();

                if (this.SelectedObjects.Contains(item))
                    txt = "" + this.SelectCharacter + this.FillCharacter + txt;

                var label = new Label(0, 0, txt);
                var width = this.GetWidth();

                if (this.BorderRight)
                    width--;

                if (this.BorderLeft)
                    width--;

                label.SetWidth(width);

                if (this.BorderRight)
                    label.AddBorder("right");

                if (this.BorderLeft)
                {
                    label.AddBorder("left");
                }
                
                foreach (string lblText in label.ToTextRepresentation())
                {
                    text.Add(lblText);
                }

                currentItemIndex++;
            }

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