using System;
using System.Windows.Forms;

namespace WindowsBleMesh
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try 
            {
                Application.SetHighDpiMode(HighDpiMode.SystemAware);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new ChatForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application crashed: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
