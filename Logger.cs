using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DrugiProjekatSistemskoProgramiranje
{
    class Logger
    {
        private static readonly object lockObj =
            new object();

        public static void Log(string msg)
        {
            lock (lockObj)
            {
                /*File.AppendAllText(
                    "server.log",
                    $"{DateTime.Now} {msg}\n"
                );*/
                Console.WriteLine(msg);
            }
        }
    }
}
