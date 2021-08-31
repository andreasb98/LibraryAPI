using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace LibraryAPI.Model
{
    
    [Table("Book")]
    public partial class Book
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }
        [Column("title")]
        [StringLength(50)]
        public string Title { get; set; }
        [Column("author")]
        [StringLength(50)]
        public string Author { get; set; }
        [Column("publisher")]
        [StringLength(50)]
        public string Publisher { get; set; }
        [Column("isAvail")]
        public bool? IsAvail { get; set; }
    }
}
