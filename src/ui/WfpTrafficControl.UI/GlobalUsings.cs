// Global using directives to resolve namespace ambiguity between WPF and Windows Forms
// WPF types are preferred over Windows Forms types in this project

global using Application = System.Windows.Application;
global using UserControl = System.Windows.Controls.UserControl;
global using Window = System.Windows.Window;
global using MessageBox = System.Windows.MessageBox;
global using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
global using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
