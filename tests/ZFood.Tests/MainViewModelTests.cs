using ZFood.App.Services;
using ZFood.App.ViewModels;
using ZFood.Core;

namespace ZFood.Tests;

public class MainViewModelTests
{
    [Fact]
    public void Failed_settings_save_surfaces_the_status_warning()
    {
        // The settings store reports failure without throwing; saving through
        // the view model must turn that into the quiet status-bar warning.
        var services = AppServices.Create(new AppPaths("/proc/zfood-cannot-write-here"));
        var vm = new MainViewModel(services);

        vm.SaveSettings();

        Assert.True(vm.StatusNoticeIsWarning);
        Assert.Contains("save settings", vm.StatusNotice);
    }

    [Fact]
    public void Successful_settings_save_stays_quiet()
    {
        using var dir = new TempDir();
        var services = AppServices.Create(new AppPaths(dir.Path));
        var vm = new MainViewModel(services);

        vm.SaveSettings();

        Assert.False(vm.StatusNoticeIsWarning);
        Assert.Equal("", vm.StatusNotice);
    }
}
