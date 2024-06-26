﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    [Table("FilesLib")]
    public partial class FilesLib
    {
        [Key]
        public int Id { get; set; }
        [StringLength(50)]
        public string FileCode { get; set; }
        [StringLength(50)]
        public string Account { get; set; }
        [StringLength(500)]
        public string FileName { get; set; }
        [StringLength(500)]
        public string FilePath { get; set; }
        [StringLength(50)]
        public string FileType { get; set; }
        [StringLength(500)]
        public string ObjectPath { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime? CreateTime { get; set; }
    }
}