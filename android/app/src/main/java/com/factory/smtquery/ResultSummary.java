package com.factory.smtquery;

import java.util.*;

/** One material per scanned DID; retain distinct locations and descriptions. */
public final class ResultSummary {
 public String packing,bpn,qty,description,station;
 public final List<String> locations=new ArrayList<>();
 public static ResultSummary from(List<QueryService.Match> rows) {
  if(rows==null||rows.isEmpty())throw new IllegalArgumentException("没有可显示的查询结果。");
  ResultSummary r=new ResultSummary();QueryService.Match first=rows.get(0);
  r.packing=first.packing;r.bpn=first.bpn;r.qty=blank(first.qty)?"未提供":first.qty;
  Set<String> stations=new LinkedHashSet<>(),descriptions=new LinkedHashSet<>(),locations=new LinkedHashSet<>();
  for(QueryService.Match x:rows){if(!blank(x.station))stations.add(x.station);if(!blank(x.description))descriptions.add(x.description);}
  for(QueryService.Match x:rows){String location=blank(x.location)?"未提供站位":x.location;if(stations.size()>1)location+=" · "+(blank(x.station)?"未提供工序":x.station);locations.add(location);}
  r.locations.addAll(locations);r.station=String.join(" / ",stations);r.description=String.join("；",descriptions);return r;
 }
 static boolean blank(String s){return s==null||s.trim().isEmpty();}
}
