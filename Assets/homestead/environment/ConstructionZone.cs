﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using RedHomestead.Simulation;
using RedHomestead.Buildings;

public class ConstructionZone : MonoBehaviour {
    public Module UnderConstruction;
    public Transform ModulePrefab;

    public float CurrentProgressSeconds = 0;
    private float RequiredProgressSeconds = 10f;
    public float ProgressPercentage
    {
        get
        {
            return CurrentProgressSeconds / RequiredProgressSeconds;
        }
    }

    internal static ConstructionZone CurrentZone;
    internal Dictionary<Matter, int> ResourceCount;
    internal List<ResourceComponent> ResourceList;
    internal bool CanConstruct { get; private set; }
    internal Matter[] RequiredResourceMask;

	// Use this for initialization
	void Start () {
        InitializeRequirements();
	}
	
	// Update is called once per frame
	//void Update () {
	//
	//}

    public void Initialize(Module toBuild)
    {
        this.UnderConstruction = toBuild;
        ModulePrefab = PrefabCache<Module>.Cache.GetPrefab(toBuild);

        InitializeRequirements();
    }
    
    public void InitializeRequirements()
    {
        if (UnderConstruction != Module.Unspecified)
        {
            ResourceCount = new Dictionary<Matter, int>();
            ResourceList = new List<ResourceComponent>();
            //todo: change to Construction.Requirements[underconstruction].keys when that's a dict of <resource, entry> and not a list
            RequiredResourceMask = new Matter[Construction.Requirements[this.UnderConstruction].Count];

            int i = 0;
            foreach(ResourceEntry required in Construction.Requirements[this.UnderConstruction])
            {
                ResourceCount[required.Type] = 0;
                RequiredResourceMask[i] = required.Type;
                i++;
            }

            //todo: move the pylons and tape to match the width/length of the module to be built
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (ResourceCount != null)
        {
            if (other.CompareTag("Player"))
            {
                GuiBridge.Instance.ShowConstruction(Construction.Requirements[this.UnderConstruction], ResourceCount, this.UnderConstruction);
                CurrentZone = this;
            }
            else if (other.CompareTag("movable"))
            {
                ResourceComponent addedResources = other.GetComponent<ResourceComponent>();
                //todo: bug: adds resources that aren't required, and surplus resources
                if (addedResources != null && !addedResources.IsInConstructionZone)
                {
                    ResourceCount[addedResources.ResourceType] += addedResources.Quantity;
                    ResourceList.Add(addedResources);
                    addedResources.IsInConstructionZone = true;
                    RefreshCanConstruct();
                    GuiBridge.Instance.ShowConstruction(Construction.Requirements[this.UnderConstruction], ResourceCount, this.UnderConstruction);
                }
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (ResourceCount != null)
        {
            if (other.CompareTag("Player"))
            {
                GuiBridge.Instance.HideConstruction();
                CurrentZone = null;
            }
            else if (other.CompareTag("movable"))
            {
                ResourceComponent removedResources = other.GetComponent<ResourceComponent>();
                //todo: bug: removes resources that aren't required, and surplus resources
                if (removedResources != null && removedResources.IsInConstructionZone)
                {
                    ResourceCount[removedResources.ResourceType] -= removedResources.Quantity;
                    ResourceList.Remove(removedResources);
                    removedResources.IsInConstructionZone = false;
                    RefreshCanConstruct();
                    GuiBridge.Instance.ShowConstruction(Construction.Requirements[this.UnderConstruction], ResourceCount, this.UnderConstruction);
                }
            }
        }
    }

    private void RefreshCanConstruct()
    {
        CanConstruct = true;

        foreach(ResourceEntry resourceEntry in Construction.Requirements[this.UnderConstruction])
        {
            if (ResourceCount[resourceEntry.Type] < resourceEntry.Count)
            {
                print("missing " + (resourceEntry.Count - ResourceCount[resourceEntry.Type]) + " " + resourceEntry.Type.ToString());
                CanConstruct = false;
                break;
            }
        }
    }

    public void WorkOnConstruction(float constructionTime)
    {
        if (CanConstruct)
        {
            this.CurrentProgressSeconds += constructionTime;

            if (this.CurrentProgressSeconds >= this.RequiredProgressSeconds)
            {
                this.Complete();
            }
        }
    }

    public void Complete()
    {
        //todo: move player out of the way
        //actually, we _should_ only be able to complete construction when the player
        //is outside the zone looking in, so maybe not
        
        GameObject.Instantiate(ModulePrefab, this.transform.position, this.transform.rotation);
        if (CurrentZone == this)
        {
            CurrentZone = null;
            GuiBridge.Instance.HideConstruction();
        }

        //todo: make this more efficient
        //what this code is doing:
        //only destroying those entries in the ResourceList that are required to build the Module
        //so you can't put in 100 steel to something that requires 10 and lose 90 excess steel
        Dictionary<Matter, int> deletedCount = new Dictionary<Matter, int>();
        for(int i = this.ResourceList.Count - 1; i >= 0; i--)
        {
            ResourceComponent component = this.ResourceList[i];
            //tell the component it isn't in a construction zone
            //just in case it will live through the rest of this method
            //(this frees it for use in another zone)
            component.IsInConstructionZone = false;

            if (RequiredResourceMask.Contains(component.ResourceType))
            {
                int numDeleted = 0;
                if (deletedCount.ContainsKey(component.ResourceType))
                {
                    numDeleted = deletedCount[component.ResourceType];
                    deletedCount[component.ResourceType] = 0;
                }

                if (numDeleted < Construction.Requirements[this.UnderConstruction].Where(r => r.Type == component.ResourceType).Count())
                {
                    this.ResourceList.Remove(component);
                    Destroy(component.gameObject);
                }
            }
        }

        Destroy(this.gameObject);
    }
}
