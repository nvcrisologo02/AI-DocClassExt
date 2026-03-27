using System.Windows;

namespace DocumentIA.Desktop
{
    public partial class App : Application
    {
        public App()
        {
            DispatcherUnhandledException += (s, e) =>
            {
                MessageBox.Show($"Dispatcher Exception:\n{e.Exception?.Message}\n\n{e.Exception?.StackTrace}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = false;
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                MessageBox.Show($"Global Exception:\n{ex?.Message}\n\n{ex?.StackTrace}", 
                    "Error Fatal", MessageBoxButton.OK, MessageBoxImage.Error);
            };

            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"InitializeComponent Error:\n{ex.Message}\n\n{ex.StackTrace}", 
                    "Error en Inicialización", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
    }
}
