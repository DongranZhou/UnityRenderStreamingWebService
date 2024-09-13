using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Disconnection
{
    public string id { get; set; }
    public long datetime { get; set; }
    public Disconnection() { }
    public Disconnection(string id,long datetime) 
    { 
        this.id = id;
        this.datetime = datetime;
    }
}
