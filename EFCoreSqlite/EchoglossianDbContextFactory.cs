using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System;
using System.Diagnostics;
using System.IO;

namespace Echoglossian.EFCoreSqlite
{

  public class EchoglossianDbContextFactory : IDesignTimeDbContextFactory<EchoglossianDbContext>
  {
    public EchoglossianDbContext CreateDbContext(string[] args)
    {

      string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
      string fullPath = Path.Combine(appDataPath, "XIVLauncher", "pluginConfigs", "Echoglossian");

      var configDir = fullPath; /*Echoglossian.PluginInterface.GetPluginConfigDirectory() + Path.DirectorySeparatorChar;*/
      return new EchoglossianDbContext(configDir);
    }
  }
}