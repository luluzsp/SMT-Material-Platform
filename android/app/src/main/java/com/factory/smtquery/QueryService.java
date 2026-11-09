package com.factory.smtquery;

import org.json.*;
import java.io.*;
import java.net.*;
import java.util.*;

/** MES/WMS gateway. Every public operation has a no-network demo branch. */
public final class QueryService {
 public interface Transport {
  String get(String url, int timeout) throws Exception;
  default String post(String url,String body,int timeout)throws Exception{return QueryService.httpPost(url,body,timeout);}
 }
 public interface Cache { void save(Order order, String raw, boolean demo) throws Exception; }

 public static final class Config {
  public String mes="http://10.131.30.150:20010/MES2/Service.action";
  public String environment="PRODUCTION";
  public String wmsTest="http://10.12.5.188:20016";
  public String wmsProduction="http://10.128.30.78:20018";
  public String warehouseId="20117901";
  public String factory="CT21"; public int inventoryPageSize=10000;
  public int timeout=15; public boolean demo=false; public final List<Line> lines=new ArrayList<>();
  public Config(){lines.add(new Line("H5-2F-S01","B"));lines.add(new Line("H5-2F-S02","T"));}
  public void validate()throws Exception{
   if(!demo)for(String s:new String[]{mes,wmsTest,wmsProduction}){try{URI u=new URI(s);if(!("http".equals(u.getScheme())||"https".equals(u.getScheme()))||u.getHost()==null||u.getQuery()!=null||u.getFragment()!=null)throw new Exception();}catch(Exception ex){throw new Exception("请填写有效的 MES/WMS 接口地址，不含问号后的参数或片段。");}}
   if(!"TEST".equals(environment)&&!"PRODUCTION".equals(environment))throw new Exception("请选择测试环境或正式环境。");
   if(timeout<1||timeout>120)throw new Exception("超时时间须为 1～120 秒。");
   if(warehouseId.trim().isEmpty())throw new Exception("WMS 仓库 ID 不能为空。");
   if(factory.trim().isEmpty())throw new Exception("WMS 工厂代码不能为空。");if(inventoryPageSize<1||inventoryPageSize>10000)throw new Exception("储位查询每页记录数须为 1～10000。");
   if(lines.isEmpty())throw new Exception("请至少配置一条线体信息。");Set<String> unique=new HashSet<>();
   for(Line l:lines){if(l.name.trim().isEmpty())throw new Exception("线体名称不能为空。");if(!unique.add(l.name.trim().toUpperCase(Locale.ROOT)))throw new Exception("线体不能重复配置。");}
  }
  public String activeWms(){return "TEST".equals(environment)?wmsTest:wmsProduction;}
  public String wmsQueryUrl()throws Exception{return origin(activeWms())+"/wms3.0/wms/exi/wmsMaterialStore/getListByPackingNum";}
  public String wmsIssueUrl()throws Exception{return origin(activeWms())+"/wms3.0/wms/exi/wmsMoveStorage/getInfoAndMoveStorage";}
  public String wmsInventoryUrl()throws Exception{return origin(activeWms())+"/wms3.0/ms/exi/wmsMaterialStore/getPage";}
  public JSONObject json()throws JSONException{JSONObject j=new JSONObject().put("mes",mes).put("environment",environment).put("wmsTest",wmsTest).put("wmsProduction",wmsProduction).put("warehouseId",warehouseId).put("factory",factory).put("inventoryPageSize",inventoryPageSize).put("timeout",timeout).put("demo",demo);JSONArray a=new JSONArray();for(Line l:lines)a.put(new JSONObject().put("line",l.name).put("suffix",l.suffix));return j.put("lines",a);}
  public static Config parse(String raw)throws JSONException{JSONObject j=new JSONObject(raw);Config c=new Config();c.mes=j.getString("mes");String legacy=j.optString("wms","");c.environment=j.optString("environment","PRODUCTION");c.wmsTest=j.optString("wmsTest",c.wmsTest);c.wmsProduction=j.optString("wmsProduction",legacy.isEmpty()?c.wmsProduction:legacy);c.warehouseId=j.optString("warehouseId","20117901");c.factory=j.optString("factory","CT21");c.inventoryPageSize=j.optInt("inventoryPageSize",10000);c.timeout=j.getInt("timeout");c.demo=j.optBoolean("demo");c.lines.clear();JSONArray a=j.getJSONArray("lines");for(int i=0;i<a.length();i++){JSONObject l=a.getJSONObject(i);c.lines.add(new Line(l.getString("line"),l.optString("suffix","")));}return c;}
 }
 public static final class Line{public final String name,suffix;public Line(String n,String s){name=n.trim();suffix=s.trim();}}
 public static final class Order{public final String line,sap,mes;public Order(Line l,String s){line=l.name;sap=s.trim();mes=sap+l.suffix;}}
 public static final class Orders{public final List<Order> values=new ArrayList<>();public final List<String> warnings=new ArrayList<>();}
 public static final class Match{public String packing,bpn,qty,location,description,station;}
 public static final class LoadInfo{public String did,bpn,shoporder,line,location;public double packageQty;}
 public static final class IssueMaterials{
  public final List<String> values=new ArrayList<>();
  public boolean contains(String material){if(material==null)return false;for(String value:values)if(value.equalsIgnoreCase(material.trim()))return true;return false;}
  public boolean hasAlternatives(){return values.size()>1;}
 }
 public static final class Shortage{
  public String id,originalDid,shoporder,line,location,mat;public double demand,issued,remaining;
  public Shortage copy(){Shortage x=new Shortage();x.id=id;x.originalDid=originalDid;x.shoporder=shoporder;x.line=line;x.location=location;x.mat=mat;x.demand=demand;x.issued=issued;x.remaining=remaining;return x;}
 }
 public static final class PackageInfo{public String packing,bpn;public double qty;public boolean substitute,moved;public String moveError="";}
 public static final class StorageLocation{public String storeLocation,storeCode;public double availableQty;}
 public static final class MaterialLocations{public String material,error="";public final List<StorageLocation> locations=new ArrayList<>();public double total;}
 public static final class IssueResult{
  public boolean completed;public final List<String> failures=new ArrayList<>();
  public String message(){if(completed)return "发料记录提交成功";return "以下包装发料失败，成功包装不会重复调用：\n"+String.join("\n",failures);}
 }

 final Config config;final Transport transport;final Cache cache;
 public QueryService(Config c,Cache cache){this(c,cache,QueryService::http);}
 public QueryService(Config c,Cache cache,Transport t){config=c;this.cache=cache;transport=t;}
 static String http(String address,int timeout)throws Exception{
  HttpURLConnection c=(HttpURLConnection)new URL(address).openConnection();c.setRequestMethod("GET");c.setConnectTimeout(timeout*1000);c.setReadTimeout(timeout*1000);c.setInstanceFollowRedirects(false);
  try{int code=c.getResponseCode();if(code<200||code>=300)throw new IOException("HTTP "+code);try(InputStream in=c.getInputStream();ByteArrayOutputStream bytes=new ByteArrayOutputStream()){byte[] b=new byte[8192];int n;long deadline=System.nanoTime()+timeout*1_000_000_000L;while((n=in.read(b))!=-1){if(Thread.currentThread().isInterrupted())throw new IOException("操作已取消");if(System.nanoTime()>deadline)throw new SocketTimeoutException();if(bytes.size()+n>16*1024*1024)throw new IOException("接口响应超过 16 MB");bytes.write(b,0,n);}return bytes.toString("UTF-8");}}finally{c.disconnect();}
 }
 static String httpPost(String address,String body,int timeout)throws Exception{
  HttpURLConnection c=(HttpURLConnection)new URL(address).openConnection();c.setRequestMethod("POST");c.setConnectTimeout(timeout*1000);c.setReadTimeout(timeout*1000);c.setInstanceFollowRedirects(false);c.setDoOutput(true);c.setRequestProperty("Content-Type","application/json; charset=UTF-8");
  try{byte[] payload=body.getBytes("UTF-8");c.setFixedLengthStreamingMode(payload.length);try(OutputStream out=c.getOutputStream()){out.write(payload);}int code=c.getResponseCode();InputStream source=code>=200&&code<300?c.getInputStream():c.getErrorStream();if(source==null)throw new IOException("HTTP "+code);try(InputStream in=source;ByteArrayOutputStream bytes=new ByteArrayOutputStream()){byte[] b=new byte[8192];int n;while((n=in.read(b))!=-1){if(Thread.currentThread().isInterrupted())throw new IOException("操作已取消");if(bytes.size()+n>16*1024*1024)throw new IOException("接口响应超过 16 MB");bytes.write(b,0,n);}String text=bytes.toString("UTF-8");if(code<200||code>=300)throw new IOException("HTTP "+code+" "+text);return text;}}finally{c.disconnect();}
 }
 static String origin(String address)throws Exception{String value=address.trim();if(!value.startsWith("http://")&&!value.startsWith("https://"))value="http://"+value;URI u=new URI(value);if(u.getHost()==null)throw new Exception("WMS接口地址无效");return u.getScheme()+"://"+u.getAuthority();}
 public static String url(String base,String... pairs)throws Exception{StringBuilder b=new StringBuilder(base).append('?');for(int i=0;i<pairs.length;i+=2){if(i>0)b.append('&');b.append(URLEncoder.encode(pairs[i],"UTF-8")).append('=').append(URLEncoder.encode(pairs[i+1],"UTF-8"));}return b.toString();}
 String get(String address,String name)throws Exception{try{return transport.get(address,config.timeout);}catch(SocketTimeoutException e){throw new Exception(name.equals("getListByPackingNum")?"调用WMS系统getListByPackingNum接口信息超时":"获取"+name+"接口信息超时");}catch(IOException e){throw new Exception(name+"接口连接失败，请检查网络、接口地址和服务状态。",e);}}
 String post(String address,String body,String name)throws Exception{try{return transport.post(address,body,config.timeout);}catch(SocketTimeoutException e){throw new Exception("调用WMS系统"+name+"接口信息超时");}catch(IOException e){throw new Exception(name+"接口连接失败，请检查网络、接口地址和服务状态。",e);}}
 static JSONObject decode(String raw)throws Exception{try{JSONObject j=new JSONObject(raw);if(j.optString("RESULT","").isEmpty())throw new JSONException("RESULT missing");return j;}catch(JSONException e){throw new Exception("接口响应格式无效，缺少 RESULT 或 JSON 无法解析。");}}
 String mes(String method,JSONObject p)throws Exception{p.put("LOGIN_ID","-1").put("CLIENT_ID","1");return get(url(config.mes,"method",method,"param",p.toString()),method);}
 String mesPost(String method,JSONObject p)throws Exception{
  p.put("LOGIN_ID",-1).put("CLIENT_ID",1);String address=url(config.mes,"method",method,"param",p.toString());
  try{return transport.post(address,"",config.timeout);}catch(SocketTimeoutException e){throw new Exception("调用MES系统"+method+"接口信息超时");}catch(IOException e){throw new Exception(method+"接口连接失败，请检查网络、接口地址和服务状态。",e);}
 }
 static void pass(JSONObject j,String fallback)throws Exception{if(!"PASS".equalsIgnoreCase(j.optString("RESULT"))){String m=j.optString("MESSAGE","").trim();throw new Exception(m.isEmpty()?fallback:m);}}
 static double number(JSONObject j,String key){if(j==null||j.isNull(key))return 0;Object value=j.opt(key);if(value instanceof Number)return ((Number)value).doubleValue();try{return Double.parseDouble(String.valueOf(value));}catch(Exception e){return 0;}}
 public static String amount(double n){if(Math.abs(n-Math.rint(n))<0.000001)return String.valueOf((long)Math.rint(n));return java.math.BigDecimal.valueOf(n).stripTrailingZeros().toPlainString();}

 public Orders orders()throws Exception{
  config.validate();Orders result=new Orders();
  for(Line l:config.lines){if(Thread.currentThread().isInterrupted())throw new IOException("操作已取消");try{JSONObject j=config.demo?new JSONObject("{\"RESULT\":\"PASS\",\"SHOPORDER_LIST\":[\"4311562578\"]}"):decode(mes("GetShoporderByLine",new JSONObject().put("LINE",l.name)));JSONArray a=j.optJSONArray("SHOPORDER_LIST");if("PASS".equals(j.optString("RESULT"))&&a!=null){Set<String> seen=new HashSet<>();for(int i=0;i<a.length();i++){String sap=a.isNull(i)?"":a.getString(i).trim();if(!sap.isEmpty()&&seen.add(sap))result.values.add(new Order(l,sap));}}else result.warnings.add(l.name+"："+j.optString("MESSAGE","未找到工单"));}catch(Exception e){result.warnings.add(l.name+"："+e.getMessage());}}
  if(result.values.isEmpty())throw new Exception("根据维护的线体信息没有找到已排产的工单信息\n"+String.join("\n",result.warnings));return result;
 }
 public JSONObject load(Order order)throws Exception{String raw=config.demo?demoLoad():mes("GetLoadByShoporderAndLine",new JSONObject().put("LINE",order.line).put("SHOPORDER",order.mes));JSONObject r=decode(raw),l=r.optJSONObject("LOAD");if(!"PASS".equals(r.optString("RESULT"))||l==null||l.optJSONArray("LOAD_SUBS")==null||l.getJSONArray("LOAD_SUBS").length()==0)throw new Exception("根据线体信息"+order.line+"和工单"+order.mes+"没有找到已排产的工单信息");cache.save(order,raw,config.demo);return l;}
 public List<Match> scan(String did,Order order,JSONObject load)throws Exception{
  did=did.trim();if(did.isEmpty())throw new Exception("请扫描或输入 DID 包装编号。");if(order==null||load==null)throw new Exception("请先选择工单并加载站位表。");
  JSONObject r=decode(config.demo?demoWms(did):get(url(config.wmsQueryUrl(),"packingNum",did,"warehouseId",config.warehouseId),"getListByPackingNum"));JSONArray data=r.optJSONArray("DATA");
  if(!"PASS".equals(r.optString("RESULT"))||data==null||data.length()==0||data.optJSONObject(0)==null)throw new Exception("包装编号:"+did+"在WMS系统没有找到对应信息");JSONObject m=data.getJSONObject(0);String bpn=string(m,"bpn");
  if(bpn.trim().isEmpty())throw new Exception("包装编号:"+did+"在WMS系统的bpn信息为空");List<Match> out=new ArrayList<>();JSONArray slots=load.getJSONArray("LOAD_SUBS");
  for(int i=0;i<slots.length();i++){JSONObject s=slots.optJSONObject(i);if(s!=null&&bpn.equals(s.optString("bydpn"))){Match x=new Match();x.packing=string(m,"packingNum");if(x.packing.trim().isEmpty())x.packing=did;x.bpn=bpn;x.qty=string(m,"qty");x.location=string(s,"location");x.description=string(s,"description");x.station=string(s,"station");out.add(x);}}
  if(out.isEmpty())throw new Exception("包装编号:"+did+"在WMS系统的bpn："+bpn+"在工单"+order.mes+"的站位表"+load.optString("LOAD")+"中找不到");return out;
 }

 public LoadInfo loadDid(String did)throws Exception{
  did=did.trim();if(did.isEmpty())throw new Exception("请扫描或输入 DID 包装编号。");JSONObject r=config.demo?demoLoadDid(did):decode(mes("GetLoadDidInfo",new JSONObject().put("DID",did)));pass(r,"未找到该包装编号的上料信息。");JSONObject d=r.optJSONObject("LoadUp");if(d==null)throw new Exception("包装编号："+did+"没有对应的上料信息。");
  LoadInfo x=new LoadInfo();x.did=string(d,"did");if(x.did.isEmpty())x.did=did;x.bpn=string(d,"bydpn");x.shoporder=string(d,"shoporder");x.line=string(d,"line");x.location=string(d,"location");
  if(x.bpn.isEmpty()||x.shoporder.isEmpty()||x.line.isEmpty()||x.location.isEmpty())throw new Exception("上料信息不完整，缺少料号、工单、线体或站位。");
  PackageInfo packageInfo=packageInfo(x.did);if(!x.bpn.equalsIgnoreCase(packageInfo.bpn))throw new Exception("包装编号："+x.did+"在MES中的料号"+x.bpn+"与WMS中的料号"+packageInfo.bpn+"不一致。");x.packageQty=packageInfo.qty;return x;
 }
 public String submitShortage(LoadInfo x,int reels)throws Exception{
  if(x==null)throw new Exception("请先查询缺料包装信息。");if(reels<1)throw new Exception("需求盘数必须为大于 0 的整数。");if(x.packageQty<=0)throw new Exception("WMS返回的单盘数量无效，请重新查询。");double demand=x.packageQty*reels;if(config.demo)return x.line+"线体，"+x.location+"站位，需求"+amount(demand)+"，物料叫料成功（演示）";
  JSONObject p=new JSONObject().put("ORIGINAL_DID",x.did).put("MAT",x.bpn).put("LINE",x.line).put("SHOPORDER",x.shoporder).put("Location",x.location).put("DEMAND_QTY",amount(demand));JSONObject r=decode(mes("MatShortage",p));pass(r,"物料叫料失败，请重试。");return x.line+"线体，"+x.location+"站位，需求"+amount(demand)+"，物料叫料成功";
 }
 public String submitShortage(LoadInfo x)throws Exception{return submitShortage(x,1);}
 public List<Shortage> shortages()throws Exception{
  if(config.demo)return demoShortages();JSONObject r=decode(mes("MatShortageInfo",new JSONObject()));pass(r,"获取待发料明细失败。");JSONArray data=r.optJSONArray("DATA");if(data==null||data.length()==0)throw new Exception("SMT产线目前没有叫料信息");List<Shortage> out=new ArrayList<>();
  for(int i=0;i<data.length();i++){JSONObject d=data.optJSONObject(i);if(d==null)continue;Shortage x=new Shortage();x.id=string(d,"ID");x.originalDid=string(d,"ORIGINAL_DID");x.shoporder=string(d,"SHOPORDER");x.line=string(d,"LINE");x.location=string(d,"LOCATION");x.mat=string(d,"MAT");x.demand=number(d,"DEMAND_QTY");x.issued=0;x.remaining=x.demand;out.add(x);}if(out.isEmpty())throw new Exception("SMT产线目前没有叫料信息");return out;
 }
 public IssueMaterials issueMaterials(Shortage shortage)throws Exception{
  if(shortage==null)throw new Exception("请先选择一条待发料明细。");
  if(shortage.shoporder.trim().isEmpty()||shortage.line.trim().isEmpty()||shortage.location.trim().isEmpty())throw new Exception("发料任务缺少工单、线体或站位信息，无法查询替代料。");
  String raw=config.demo?demoIssueMaterials(shortage):mesPost("GetLoadByShoporderAndLine",new JSONObject().put("SHOPORDER",shortage.shoporder).put("LINE",shortage.line));
  JSONObject response=decode(raw);pass(response,"替代料查询失败。");JSONObject load=response.optJSONObject("LOAD");JSONArray subs=load==null?null:load.optJSONArray("LOAD_SUBS");
  if(subs==null||subs.length()==0)throw new Exception("工单"+shortage.shoporder+"、线体"+shortage.line+"没有返回可发料站位信息。");
  IssueMaterials result=new IssueMaterials();Set<String> seen=new HashSet<>();
  for(int i=0;i<subs.length();i++){JSONObject item=subs.optJSONObject(i);if(item==null||!shortage.location.trim().equalsIgnoreCase(string(item,"location").trim()))continue;String material=string(item,"bydpn").trim();if(!material.isEmpty()&&seen.add(material.toUpperCase(Locale.ROOT)))result.values.add(material);}
  if(result.values.isEmpty())throw new Exception("工单"+shortage.shoporder+"、线体"+shortage.line+"的站位"+shortage.location+"没有找到可发料号。");
  return result;
 }
 public PackageInfo packageInfo(String packing)throws Exception{
  packing=packing.trim();if(packing.isEmpty())throw new Exception("请扫描或输入发料包装编号。");JSONObject r=config.demo?demoPackage(packing):decode(get(url(config.wmsQueryUrl(),"packingNum",packing,"warehouseId",config.warehouseId),"getListByPackingNum"));pass(r,"包装编号:"+packing+"在WMS系统没有找到对应信息");JSONArray a=r.optJSONArray("DATA");JSONObject d=a==null?null:a.optJSONObject(0);if(d==null)throw new Exception("包装编号:"+packing+"在WMS系统没有找到对应信息");PackageInfo x=new PackageInfo();x.packing=string(d,"packingNum");if(x.packing.isEmpty())x.packing=packing;x.bpn=string(d,"bpn");x.qty=number(d,"qty");if(x.bpn.isEmpty())throw new Exception("包装编号："+packing+"的bpn为空。");if(x.qty<=0)throw new Exception("包装编号："+packing+"的可发数量无效。");return x;
 }
 public MaterialLocations storageLocations(String material)throws Exception{
  MaterialLocations result=new MaterialLocations();result.material=material.trim();if(result.material.isEmpty())throw new Exception("储位查询料号不能为空。");
  JSONObject request=new JSONObject().put("factory",config.factory).put("warehouseId",config.warehouseId).put("mergeBpn",result.material).put("storeStatus","已上架").put("pageSize",config.inventoryPageSize);
  JSONObject response=config.demo?demoLocations(result.material):decode(post(config.wmsInventoryUrl(),request.toString(),"getPage"));pass(response,"WMS储位查询失败");JSONObject data=response.optJSONObject("DATA");JSONArray page=data==null?null:data.optJSONArray("page");if(page==null)return result;
  Map<String,StorageLocation> grouped=new LinkedHashMap<>();
  for(int i=0;i<page.length();i++){JSONObject item=page.optJSONObject(i);if(item==null)continue;String returnedMaterial=string(item,"mergeBpn");if(!returnedMaterial.isEmpty()&&!result.material.equalsIgnoreCase(returnedMaterial))continue;double qty=number(item,"warehouseAvailableQty");if(qty<=0)continue;String storage=string(item,"storeLocation").trim(),code=string(item,"storeCode").trim();String key=code.isEmpty()?"NO_CODE:"+i:code.toUpperCase(Locale.ROOT);StorageLocation location=grouped.get(key);if(location==null){location=new StorageLocation();location.storeLocation=storage;location.storeCode=code;grouped.put(key,location);}else if(!storage.isEmpty()&&!Arrays.asList(location.storeLocation.split(",")).contains(storage)){location.storeLocation=location.storeLocation.isEmpty()?storage:location.storeLocation+","+storage;}location.availableQty+=qty;result.total+=qty;}
  result.locations.addAll(grouped.values());
  result.locations.sort((a,b)->Double.compare(b.availableQty,a.availableQty));return result;
 }
 public IssueResult postIssue(Shortage shortage,List<PackageInfo> packages,String userName)throws Exception{
  IssueMaterials materials=new IssueMaterials();if(shortage!=null&&!shortage.mat.trim().isEmpty())materials.values.add(shortage.mat);return postIssue(shortage,packages,userName,materials);
 }
 public IssueResult postIssue(Shortage shortage,List<PackageInfo> packages,String userName,IssueMaterials materials)throws Exception{
  if(shortage==null||packages==null||packages.isEmpty())throw new Exception("请至少扫描一个发料包装编号。");if(userName==null||userName.trim().isEmpty())throw new Exception("登录工号不能为空。");
  if(materials==null||materials.values.isEmpty())throw new Exception("可发料号清单尚未加载，请返回后重新进入扫描发料界面。");
  for(PackageInfo p:packages){if(!materials.contains(p.bpn))throw new Exception("扫描的包装编号"+p.packing+"的料号"+p.bpn+"不在当前站位可发料号清单中");p.substitute=!shortage.mat.equalsIgnoreCase(p.bpn);}
  IssueResult result=new IssueResult();
  for(PackageInfo p:packages){if(p.moved)continue;try{if(config.demo){p.moved=true;p.moveError="";continue;}JSONObject request=new JSONObject().put("warehouseId",config.warehouseId).put("packingNum",p.packing).put("userName",userName.trim());JSONObject response=decode(post(config.wmsIssueUrl(),request.toString(),"getInfoAndMoveStorage"));pass(response,"WMS发料失败");p.moved=true;p.moveError="";}catch(Exception e){p.moveError=e.getMessage()==null?"未知错误":e.getMessage();result.failures.add(p.packing+"："+p.moveError);}}
  if(!result.failures.isEmpty())return result;
  StringBuilder dids=new StringBuilder();for(PackageInfo p:packages){if(dids.length()>0)dids.append(',');dids.append(p.packing);}
  if(!config.demo){JSONObject r=decode(mes("MatPost",new JSONObject().put("ID",shortage.id).put("PULL_OUT_DID",dids.toString())));pass(r,"发料记录上传失败，请重试。");}
  result.completed=true;return result;
 }

 public static String string(JSONObject j,String key){return j==null||j.isNull(key)?"":j.optString(key,"");}
 static String demoLoad()throws Exception{JSONArray a=new JSONArray();String[] loc={"4-9","5-12","4-10"};for(int i=0;i<3;i++)a.put(new JSONObject().put("bydpn",i<2?"16003372-00":"14550195-00").put("location",loc[i]).put("station","TOP_SPI").put("description",i<2?"厚膜电阻 · 150KΩ · 0402":"厚膜电阻 · 20KΩ · 0402"));return new JSONObject().put("RESULT","PASS").put("LOAD",new JSONObject().put("PROJECT","VCPB-AE10").put("LOAD","DE21(21724819-00)-TOP-AX01-S02").put("LOAD_ID",1362).put("LOAD_VERSION","B114").put("LOAD_SUBS",a)).toString();}
 static String demoWms(String did)throws Exception{if(did.equals("NOTFOUND"))return "{\"RESULT\":\"FAIL\"}";String bpn=did.equals("EMPTY")?"":did.equals("UNMATCHED")?"21536845-00":did.equals("DEMO002")?"14550195-00":"16003372-00";return new JSONObject().put("RESULT","PASS").put("DATA",new JSONArray().put(new JSONObject().put("packingNum",did).put("bpn",bpn).put("qty",200))).toString();}
 static JSONObject demoLoadDid(String did)throws Exception{return new JSONObject().put("RESULT","PASS").put("LoadUp",new JSONObject().put("did",did).put("bydpn","14382059-00").put("shoporder","4311563736T").put("line","H5-2F-S02").put("location","12-11"));}
 static String demoIssueMaterials(Shortage shortage)throws Exception{JSONArray subs=new JSONArray().put(new JSONObject().put("bydpn",shortage.mat).put("location",shortage.location).put("description","演示主料"));if("1".equals(shortage.id))subs.put(new JSONObject().put("bydpn","15435229-00").put("location",shortage.location).put("description","演示替代料"));return new JSONObject().put("RESULT","PASS").put("LOAD",new JSONObject().put("LOAD_SUBS",subs)).toString();}
 static List<Shortage> demoShortages(){List<Shortage> list=new ArrayList<>();list.add(demoShortage("1","CT2126072800001ADA1C1","5030059140T","H5-2F-S02","12-4E","13247855-00",250));list.add(demoShortage("2","126073000644561","4311563736T","H5-2F-S02","12-11","14382059-00",10000));list.add(demoShortage("3","DEMO003","5030067890T","H5-2F-S01","8-2A","13247855-00",360));return list;}
 static Shortage demoShortage(String id,String did,String order,String line,String location,String mat,double demand){Shortage x=new Shortage();x.id=id;x.originalDid=did;x.shoporder=order;x.line=line;x.location=location;x.mat=mat;x.demand=demand;x.remaining=demand;return x;}
 static JSONObject demoPackage(String packing)throws Exception{String upper=packing.toUpperCase(Locale.ROOT);String bpn=upper.contains("ALT")?"15435229-00":(upper.contains("MAT2")||"126073000644561".equals(packing))?"14382059-00":"13247855-00";double qty="126073000644561".equals(packing)?10000:packing.endsWith("2")?61:189;return new JSONObject().put("RESULT","PASS").put("DATA",new JSONArray().put(new JSONObject().put("packingNum",packing).put("bpn",bpn).put("qty",qty)));}
 static JSONObject demoLocations(String material)throws Exception{JSONArray page=new JSONArray();String[] codes=material.equals("15435229-00")?new String[]{"A001","A001","A002","B001","B001"}:new String[]{"A001","A001","B001"};for(int i=0;i<codes.length;i++)page.put(new JSONObject().put("mergeBpn",material).put("storeLocation",i<3?"DSJKW":"SMTCK").put("storeCode",codes[i]).put("warehouseAvailableQty",100));return new JSONObject().put("RESULT","PASS").put("DATA",new JSONObject().put("page",page));}
}
