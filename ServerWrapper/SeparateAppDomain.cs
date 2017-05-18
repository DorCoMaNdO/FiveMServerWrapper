using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace ServerWrapper
{
    internal static class SeparateAppDomain // Taken from "Plugin framework" on codeproject - https://www.codeproject.com/Articles/831823/Plugin-framework
    {
        private static int _counter = 0;
        private static Dictionary<string, Tuple<AppDomain, object>> _domains = new Dictionary<string, Tuple<AppDomain, object>>();

        /// <summary>
        /// Creates instance of <typeparamref name="TInstance"/> in separate AppDomain. 
        /// </summary>
        /// <typeparam name="TInstance"></typeparam>
        /// <param name="folderPath">Path to the plugin folder. Will be used for shadow copying.</param>
        /// <param name="args">Optional constructor parameters.</param>
        /// <returns></returns>
        /// <remarks>If instance with such <paramref name="folderPath"/> already exists - returns existing instance.</remarks>
        public static TInstance CreateInstance<TInstance>(string folderPath, params object[] args)
        {
            if (_domains.ContainsKey(folderPath))
            {
                return (TInstance)_domains[folderPath].Item2;
            }

            string executableFolderPath = folderPath;
            string executableFolderName = Path.GetFileName(executableFolderPath);
            string id = "ServerWrapper_" + executableFolderName + "_" + ++_counter;
            string shadowCopyFolderName = id + "_ShadowCopy";
            string shadowCopyFolderPath = CreateShadowCopyFolder(shadowCopyFolderName);

            AppDomainSetup setup = new AppDomainSetup
            {
                ShadowCopyFiles = "true",
                ShadowCopyDirectories = executableFolderPath,
                CachePath = shadowCopyFolderPath
            };

            string domainName = id + "_AppDomain";
            AppDomain domain = AppDomain.CreateDomain(domainName, AppDomain.CurrentDomain.Evidence, setup);

            Type typeToCreate = typeof(TInstance);
            TInstance instance = (TInstance)domain.CreateInstanceAndUnwrap(typeToCreate.Assembly.FullName, typeToCreate.FullName, false, BindingFlags.CreateInstance, null, args, CultureInfo.InvariantCulture, null);
            _domains.Add(folderPath, new Tuple<AppDomain, object>(domain, instance));

            return instance;
        }

        public static AppDomain Extract(string filePath)
        {
            if (!_domains.ContainsKey(filePath)) return null;

            AppDomain domain = _domains[filePath].Item1;
            _domains.Remove(filePath);
            return domain;
        }

        public static void Delete(string filePath)
        {
            if (_domains.ContainsKey(filePath))
            {
                AppDomain domain = _domains[filePath].Item1;
                _domains.Remove(filePath);
                AppDomain.Unload(domain);
            }
        }

        private static string CreateShadowCopyFolder(string shadowCopyFolderName)
        {
            string shadowCopyPath = Path.Combine(Path.GetTempPath(), shadowCopyFolderName);
            if (!Directory.Exists(shadowCopyPath))
            {
                DirectoryInfo di = Directory.CreateDirectory(shadowCopyPath);
                di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
            }
            return shadowCopyPath;
        }
    }
}