﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    [Table("UseUpLog")]
    public partial class UseUpLog
    {
        [Key]
        public int Id { get; set; }
        [StringLength(50)]
        public string ModelName { get; set; }
        [StringLength(50)]
        public string Account { get; set; }
        public int? InputCount { get; set; }
        public int? OutputCount { get; set; }
        [Column(TypeName = "money")]
        public decimal? UseMoney { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime? CreateTime { get; set; }
    }
}