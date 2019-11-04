﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Razor.RuntimeCompilation;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Westwind.AspNetCore.LiveReload;
using Westwind.Utilities;


namespace LiveReloadServer
{
    public class Startup
    {

        private string WebRoot;
        private int Port = 0;
        public bool UseLiveReload = true;
        private bool UseRazor = false;

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Get Configuration Settings
            UseLiveReload = StartupHelpers.GetLogicalSetting("LiveReloadEnabled", Configuration);
            UseRazor = StartupHelpers.GetLogicalSetting("UseRazor", Configuration);

            WebRoot = Configuration["WebRoot"];
            if (string.IsNullOrEmpty(WebRoot))
                WebRoot = Environment.CurrentDirectory;
            else
                WebRoot = Path.GetFullPath(WebRoot, Environment.CurrentDirectory);

            if (UseLiveReload)
            {
                services.AddLiveReload(opt =>
                {
                    opt.FolderToMonitor = WebRoot;
                    opt.LiveReloadEnabled = UseLiveReload;

                    var extensions = Configuration["Extensions"];
                    if (!string.IsNullOrEmpty(extensions))
                        opt.ClientFileExtensions = extensions;
                });
            }


#if USE_RAZORPAGES
if (UseRazor)
{
    services.AddRazorPages(opt => { opt.RootDirectory = "/"; })
        .AddRazorRuntimeCompilation(
            opt =>
            {
                
                opt.FileProviders.Add(new PhysicalFileProvider(WebRoot));
                LoadPrivateBinAssemblies(opt);
            });

}
#endif
        }



        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env)
        {
            bool useSsl = StartupHelpers.GetLogicalSetting("useSsl", Configuration);
            bool showUrls = StartupHelpers.GetLogicalSetting("ShowUrls", Configuration);
            bool openBrowser = StartupHelpers.GetLogicalSetting("OpenBrowser", Configuration);

            string defaultFiles = Configuration["DefaultFiles"];
            if (string.IsNullOrEmpty(defaultFiles))
                defaultFiles = "index.html,default.htm,default.html";

            var strPort = Configuration["Port"];
            if (!int.TryParse(strPort, out Port))
                Port = 5000;

            if (UseLiveReload)
                app.UseLiveReload();

            if (env.IsDevelopment())
                app.UseDeveloperExceptionPage();
            else
                app.UseExceptionHandler("/Error");

            if (showUrls)
            {
                app.Use(async (context, next) =>
                {
                    var url =
                        $"{context.Request.Method}  {context.Request.Scheme}://{context.Request.Host}  {context.Request.Path}{context.Request.QueryString}";
                    Console.WriteLine(url);
                    await next();
                });
            }

            app.UseDefaultFiles(new DefaultFilesOptions
            {
                FileProvider = new PhysicalFileProvider(WebRoot),
                DefaultFileNames = new List<string>(defaultFiles.Split(',', ';'))
            });

            app.UseStaticFiles(new StaticFileOptions
            {
                FileProvider = new PhysicalFileProvider(WebRoot),
                RequestPath = new PathString("")
            });

#if USE_RAZORPAGES
            if (UseRazor)
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => { endpoints.MapRazorPages(); });
            }
#endif
            var url = $"http{(useSsl ? "s" : "")}://localhost:{Port}";
            var extensions = Configuration["Extensions"];

            string headerLine = new string('-', Program.AppHeader.Length);
            Console.WriteLine(headerLine);
            Console.WriteLine(Program.AppHeader);
            Console.WriteLine(headerLine);
            Console.WriteLine($"(c) West Wind Technologies, 2018-{DateTime.Now.Year}\r\n");
            Console.Write($"Site Url     : ");
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(url);
            Console.ResetColor();
            Console.WriteLine($"Web Root     : {WebRoot}");
            Console.WriteLine(
                $"Extensions   : {(string.IsNullOrEmpty(extensions) ? $"{(UseRazor ? ".cshtml," : "")},.css,.js,.htm,.html,.ts" : extensions)}");
            Console.WriteLine($"Live Reload  : {UseLiveReload}");

#if USE_RAZORPAGES
            Console.WriteLine($"Use Razor    : {UseRazor}");
#endif
            Console.WriteLine($"Show Urls    : {showUrls}");
            Console.WriteLine($"Open Browser : {openBrowser}");
            Console.WriteLine($"Default Pages: {defaultFiles}");

            Console.WriteLine();
            Console.WriteLine("'LiveReloadServer --help' for start options...");
            Console.WriteLine();
            Console.WriteLine("Ctrl-C or Ctrl-Break to exit...");

            Console.WriteLine("----------------------------------------------");

            if (openBrowser)
                StartupHelpers.OpenUrl(url);

            //LoadPrivateBinAssemblies();
            ////var list = AppDomain.CurrentDomain.GetAssemblies();
            //var list = AssemblyLoadContext.Default.Assemblies;

            //try
            //{
            //    //var a = list.FirstOrDefault(a => a.FullName.Contains("Markdig"));
            //    //var t1 = a.GetType("Markdig.Markdown", true); // works
            //    //var t2 = Type.GetType("Markdig.Markdown, Markdig", true); // fails even if type is referenced by project!
            //    ////var t3 = Type.GetType("Markdig.Markdown", true); // fails even if type is referenced by project!


            //    var md = GetTypeFromName("Westwind.AspNetCore.Markdown.Markdown");
            //    md.InvokeMember("Parse", BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static, null,
            //        null, new object[] { "**asdasd**", false, false, false });
            //}
            //catch (Exception ex)
            //{

            //}


        }

        public static Type GetTypeFromName(string TypeName)
        {

            Type type = Type.GetType(TypeName);
            if (type != null)
                return type;

            // *** try to find manually
            foreach (Assembly ass in AssemblyLoadContext.Default.Assemblies)
            {
                type = ass.GetType(TypeName, false);

                if (type != null)
                    break;

            }
            return type;
        }

        private void LoadPrivateBinAssemblies(MvcRazorRuntimeCompilationOptions opt)
        {
            var binPath = Path.Combine(WebRoot, "PrivateBin");
            if (Directory.Exists(binPath))
            {
                var files = Directory.GetFiles(binPath);
                foreach (var file in files)
                {
                    if (!file.EndsWith(".dll", StringComparison.CurrentCultureIgnoreCase) &&
                       !file.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase))
                        continue;

                    try
                    {
                        //var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(file);

                        opt.AdditionalReferencePaths.Add(file);

                        //var asm = Assembly.LoadFrom(file);
                        var oldColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Additional Assembly: " + file);
                        Console.ForegroundColor = oldColor;
                    }
                    catch (Exception ex)
                    {
                        var oldColor = Console.ForegroundColor;
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Failed to load private assembly: " + file);
                        Console.WriteLine("   " + ex.Message);
                        Console.ForegroundColor = oldColor;
                    }

                }
            }

        }

    }
}
