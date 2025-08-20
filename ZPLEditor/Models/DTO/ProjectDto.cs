using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZPLEditor.Models.DTO
{
    public class ProjectDto
    {
        public string LabelName { get; set; } = string.Empty;
        public double LabelWidth { get; set; } = 100;
        public double LabelHeight { get; set; } = 100;

        public List<ElementDataDto> Elements { get; set; } = new();
    }
}
