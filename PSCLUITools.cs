using System;
using System.Management.Automation;
using System.Management.Automation.Host;
using System.Management.Automation.Runspaces;
using System.Collections.Generic;

namespace PSCLUITools
{
    [Cmdlet(VerbsCommon.New,"Menu2")]
    [OutputType(typeof(Object))]
    public class NewMenu : PSCmdlet
    {
        // Menu items
        [Parameter(
            Mandatory = false,
            Position = 0,
            ValueFromPipeline = true,
            ValueFromPipelineByPropertyName = true)]
        public List<Object> InputObject { get; set; }

        //     Default: Select one by hitting Enter
        // Multiselect: Pick multiple items with Space, select with Enter
        //        List: Display a list of items and return them
        [Parameter(Mandatory = false)]
        [ValidateSet("Multiselect","List","Default")]
        public string Mode { get; set; } = "Default";

        // The name of the property of items to be displayed on the menu, such as Name
        // NOTE Leaving this out for now. Seems that this would require baking in a list of object 
        // types, trying to cast each object as a type and then accessing the Property. If something 
        // like this is needed just replace .ToString() on each object in Powershell before passing 
        // it to this CMDLet or wrap each object in a PSObject that has it's own .ToString() method 
        // if you can't modify the original object.
        //[Parameter(Mandatory = false)]
        //public string DisplayProperty { get; set; }

        // Align item names Center of Left
        [Parameter(Mandatory = false)]
        [ValidateSet("Center","Left")]
        public string AlignText { get; set; } = "Left";

        // A title to display above the menu, a string in the list corresponds to a title line
        [Parameter(Mandatory = false)]
        public List<string> Title { get; set; } = new List<string>();

        // Align Title Center of Left
        [Parameter(Mandatory = false)]
        [ValidateSet("Center","Left")]
        public string AlignTitle { get; set; } = "Left";

        // Display a list of selected objects after multiselection
        [Parameter(Mandatory = false)]
        public SwitchParameter ListSelected { get; set; }

        // Horizontal position of the upper left corner
        [Parameter(Mandatory = false)]
        [ValidateRange(-1, int.MaxValue)] // TODO Use console buffer width as the max
        public int LeftPosition { get; set; } = -1;

        // Vertical position of the upper left corner
        [Parameter(Mandatory = false)]
        [ValidateRange(-1, int.MaxValue)] // TODO Use console buffer height as the max
        public int TopPosition { get; set; } = -1;

        // Width of the menu
        [Parameter(Mandatory = false)]
        [ValidateRange(-1, int.MaxValue)]
        public int Width { get; set; } = -1;

        // Height of the menu
        [Parameter(Mandatory = false)]
        [ValidateRange(-1, int.MaxValue)]
        public int Height { get; set; } = -1;

        // Foreground color
        [Parameter(Mandatory = false)]
        public string ItemColor { get; set; }

        // Background color
        [Parameter(Mandatory = false)]
        public string BackgroundColor { get; set; }

        // Indicates current item
        [Parameter(Mandatory = false)]
        public string ActiveItemColor { get; set; } = "Green";

        // Indicates an item is selected
        [Parameter(Mandatory = false)]
        public string SelectedItemColor { get; set; } = "Yellow";

        // Indicates a color is both current and selected
        [Parameter(Mandatory = false)]
        public string ActiveAndSelectedItemColor { get; set; } = "Magenta";

        // Background character (empty cell)
        [Parameter(Mandatory = false)]
        public char BackgroundCharacter { get; set; } = ' ';

        // Edge character
        [Parameter(Mandatory = false)]
        public char EdgeCharacter { get; set; } = '#';

        // Padding character
        [Parameter(Mandatory = false)]
        public char PaddingCharacterTop { get; set; } = ' ';

        // Padding character
        [Parameter(Mandatory = false)]
        public char PaddingCharacterRight { get; set; } = ' ';
        
        // Padding character
        [Parameter(Mandatory = false)]
        public char PaddingCharacterBottom { get; set; } = ' ';
        
        // Padding character
        [Parameter(Mandatory = false)]
        public char PaddingCharacterLeft { get; set; } = ' ';

        // Remove edge
        [Parameter(Mandatory = false)]
        public SwitchParameter NoEdge { get; set; }

        // Remove edge
        [Parameter(Mandatory = false)]
        public SwitchParameter NoPadding { get; set; }

        public List<Object> PipelineInputList = new List<Object>();

        protected override void ProcessRecord()
        {
            PipelineInputList.Add(InputObject[0]);
        }

        protected override void EndProcessing()
        {
            // FIX There's a bug in the basic Buffer that adds two extra border 
            // characters to the top border of menu when the Title label is added

            if (PipelineInputList.Count > 1)
                InputObject = PipelineInputList;

            List<Object> NewMenu()
            {
                //var buffer = new Buffer();
                var buffer = new Buffer(Host);
                int left = 0;
                int top = 0;
                //var container = new Container(left, top, Console.WindowWidth, Console.WindowHeight);
                var container = new Container(left, top, 0, 0);
                buffer.Add(container);

                var label = new Label(0, 0, Title);
                if (Title.Count > 0)
                {
                    label.AddBorder("all");
                    label.RemoveBorder("bottom");
                    label.AddPadding("left");
                    label.AddPadding("right");
                    if (AlignTitle != null)
                        label.AlignText = AlignTitle;
                    container.AddControl(label);
                }

                var menu = new Menu(0, 0, InputObject);
                menu.Mode = Mode;
                menu.AddBorder("all");
                menu.AddPadding("all");
                if (AlignText != null)
                    menu.AlignText = AlignText;

                container.AddControl(menu);

                if (LeftPosition > -1)
                    container.SetHorizontalPosition(LeftPosition);
                else
                {
                    var x = Console.WindowWidth / 2 - container.GetWidth() / 2;
                    container.SetHorizontalPosition(x);
                }

                if (TopPosition > -1)
                    container.SetVerticalPosition(TopPosition);
                else
                {
                    var y = Console.WindowHeight / 2 - container.GetHeight() / 2;
                    container.SetVerticalPosition(y);
                }
                
                buffer.UpdateAll();
                buffer.Write();
                
                Console.WriteLine(container.GetWidth());
                Console.WriteLine(menu.GetWidth());
                return menu.ReadKey();
            }

            List<Object> result = NewMenu();           

            if (result != null && Mode == "Multiselect" && ListSelected.IsPresent)
            {
                if (result.Count > 0)
                    InputObject = result;
                else
                {
                    InputObject = new List<Object>();
                    InputObject.Add("You didn't select any items");
                }

                Mode = "List";
                Title = new List<string>();
                Title.Add("Selected items");
                List<Object> output = NewMenu();

                if (result.Count > 0)
                    WriteObject(output);
            }
            else if (result != null)
                WriteObject(result);
        }
    }
}
