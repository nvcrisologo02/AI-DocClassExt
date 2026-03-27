using DocumentIA.Desktop.ViewModels;
using DocumentIA.Desktop.Views;
using System.Windows;

namespace DocumentIA.Desktop
{
    public partial class MainWindow : Window
    {
        private ProcessingViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                _viewModel = new ProcessingViewModel();
                DataContext = _viewModel;

                // Hook up property change for JSON output
                _viewModel.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(ProcessingViewModel.OutputJson) && _viewModel.OutputJson != null)
                    {
                        JsonViewer.DisplayJson(_viewModel.OutputJson);
                    }
                };
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error inicializando MainWindow:\n{ex.Message}\n\n{ex.StackTrace}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                throw;
            }
        }
    }
}
