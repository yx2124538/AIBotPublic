﻿// <auto-generated> This file has been auto generated by EF Core Power Tools. </auto-generated>
#nullable disable
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace aibotPro.Models
{
    [Table("WorkFlow")]
    public partial class WorkFlow
    {
        [Key]
        public int Id { get; set; }
        [StringLength(50)]
        public string Account { get; set; }
        [StringLength(50)]
        public string FlowCode { get; set; }
        [StringLength(50)]
        public string Pcode { get; set; }
        public string FlowJson { get; set; }
        [Column(TypeName = "datetime")]
        public DateTime? CreateTime { get; set; }
    }
}