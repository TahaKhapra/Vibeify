using System.Windows.Controls;
using VibeMP.Models;
using VibeMP.ViewModels;

namespace VibeMP.Views
{
    public partial class CategoryEditor : UserControl
    {
        public CategoryEditor()
        {
            InitializeComponent();
        }

        private void CategoryBpmInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (
                sender is TextBox textBox
                && textBox.DataContext is VibeCategory category
                && DataContext is MainViewModel viewModel
            )
            {
                viewModel.UpdateCategoryBpmDirectly(category, textBox.Text);
            }
        }
    }
}
