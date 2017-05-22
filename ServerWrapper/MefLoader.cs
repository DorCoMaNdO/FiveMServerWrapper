using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ServerWrapper
{
    /// <summary>
    /// Discovers and instantiates all classes from specified folder that implements specific MEF contract.
    /// </summary>
    internal class MefLoader : MarshalByRefObject, IDisposable // Taken from "Plugin framework" on codeproject - https://www.codeproject.com/Articles/831823/Plugin-framework
    {
        private CompositionContainer _container;

        public MefLoader(string folderPath)
        {
            FolderPath = folderPath;

            Domain = AppDomain.CurrentDomain;
        }

        public string FolderPath { get; private set; }

        public AppDomain Domain { get; private set; }

        public List<TResult> Load<TResult>() where TResult : class
        {
            List<TResult> instances = new List<TResult>();
            try
            {
                if (_container != null)
                {
                    (_container.Catalog as DirectoryCatalog).Refresh();
                    instances = _container.GetExportedValues<TResult>().ToList();
                }
                else
                {
                    DirectoryCatalog catalog = new DirectoryCatalog(FolderPath);
                    _container = new CompositionContainer(catalog);
                    instances = _container.GetExportedValues<TResult>().ToList();
                }

                return instances;
            }
            catch (ImportCardinalityMismatchException)//when no contract implementation
            {
                return instances;
            }
            catch (ReflectionTypeLoadException)//when wrong contract implementation
            {
                return instances;
            }
            catch (FileNotFoundException)//
            {
                return instances;
            }
            catch (Exception ex)
            {
                if (Wrapper.instance != null) Wrapper.instance.PrintException(ex);

                throw new FormatException("Load of MEF file failed.", ex);
            }
        }

        public void Dispose()
        {
            if (_container != null) _container.Dispose();
        }
    }
}
