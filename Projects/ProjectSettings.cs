using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LaravelLauncher.Projects
{
    public class ProjectSettings
    {
        public string Path { get; set; }
        public bool UseNpm { get; set; }
        public bool UseYarn { get; set; }
        public bool startTasks { get; set; }
    }
}
