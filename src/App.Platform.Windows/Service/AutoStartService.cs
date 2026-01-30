using System.Diagnostics;
using Core.Abstractions;
using Microsoft.Win32;

namespace App.Platform.Windows.Service;

public class AutoStartService : IAutoStartService
{
    
    private readonly string AutoRunRegPath = @"Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    public bool IsEnabled()
    {
        throw new NotImplementedException();
    }

    public void Enable()
    {
        throw new NotImplementedException();
    }

    public void Disable()
    {
        throw new NotImplementedException();
    }
}