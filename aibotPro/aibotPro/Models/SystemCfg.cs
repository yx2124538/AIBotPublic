﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    [Table("SystemCfg")]
    public partial class SystemCfg
    {
        [Key]
        public int Id { get; set; }
        [StringLength(50)]
        public string CfgCode { get; set; }
        [StringLength(100)]
        public string CfgName { get; set; }
        [StringLength(50)]
        public string CfgKey { get; set; }
        public string CfgValue { get; set; }
    }
}