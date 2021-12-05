// <copyright file="UiFontHandler.cs" company="lokinmodar">
// Copyright (c) lokinmodar. All rights reserved.
// Licensed under the Creative Commons Attribution-NonCommercial-NoDerivatives 4.0 International Public License license.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Dalamud.Logging;
using ImGuiNET;

namespace Echoglossian
{
  public partial class Echoglossian
  {
    public bool FontLoaded;
    public bool FontLoadFailed;
    public ImFontPtr UiFont;
    public readonly string CharsToAddToAll = "─━│┃┄┅┆┇┈┉┊┋┌┍┎┏┐┑┒┓└┕┖┗┘┙┚┛├┝┞┟┠┡┢┣┤┥┦┧┨┩┪┫┬┭┮┯┰┱┲┳┴┵┶┷┸┹┺┻┼┽┾┿╀╁╂╃╄╅╆╇╈╉╊╋╌╍╎╏═║╒╓╔╕╖╗╘╙╚╛╜╝╞╟╠╡╢╣╤╥╦╧╨╩╪╫╬╭╮╯╰╱╲╳╴╵╶╷╸╹╺╻╼╽╾╿▀▁▂▃▄▅▆▇█▉▊▋▌▍▎▏▐░▒▓▔▕▖▗▘▙▚▛▜▝▞▟\"︰︱︲︳︴︵︶︷︸︹︺︻︼︽︾︿﹀﹁﹂﹃﹄﹅﹆﹇﹈﹉﹊﹋﹌﹍﹎﹏、。〃〄々〆〇〈〉《》「」『』【】〒〓〔〕〖〗〘〙〚〛〜〝〞〟〠〡〢〣〤〥〦〧〨〩〪〭〮〯〫〬〰〱〲〳〴〵〶〷〸〹〺〻〼〽〾〿──᭠ ᠆֊⸗־・﹣－･᐀‧⁃⸚⹀゠𐺭··ˑ·ּ᛫•‧∘∙⋅⏺●◦⚫⦁⸰⸳⸱・ꞏ･ּ・･᛫⸰··⸱⸳𐄁•‧∘∙⋅⏺●◦⦁⚫ˑꞏ•‣⁃⁌⁍∙○◘◦☙❥❧⦾⦿«»‘’‚‛“”„‟‹›꙰꙱꙲꙼꙽꙯;·꙳꙾΄˜˘˙΅˚˝˛ʹ͵ʺ˂˃˄˅ˆˇˈˉˊˋˌˍˎˏ˒˓˔˕˖˗˞˟˥˦˧˨˩˪˫ˬ˭˯˰˱˲˳˴˵˶˷˸˹˺˻˼˽˾˿҂϶҈҉҆҅҄҇◌҃ːˑ※■ĂăǍǎǺǻǞǟȦȧǠǡĄąĀāȀȁȂȃǼǽǢǣȺḂḃƀɃƁƂƃĆćĈĉČčĊċȻȼƇƈĎďḊḋĐđȸǱǲǳǄǅǆƉƊƋƌɗȡĔĕĚěĖėȨȩĘęĒēȄȅȆȇɆɇƎǝƏəƐḞḟƑƒǴǵĞğĜĝǦǧĠġĢģǤǥƓƔˠƢƣʰĤĥȞȟĦħƕǶʱʻʽĬĭǏǐĨĩİĮįĪīȈȉȊȋĲĳıƗƖʲĴĵǰȷɈɉǨǩĶķƘƙˡĹĺĽľĻļŁłĿŀǇǈǉƚȽȴƛṀṁŃńǸǹŇňŅņǊǋǌƝƞȠȵŊŋŎŏǑǒȪȫŐőȬȭȮȯȰȱǾǿǪǫǬǭŌōȌȍȎȏƠơŒœƆƟȢȣṖṗƤƥȹɊɋĸʳŔŕŘřŖŗȐȑȒȓƦɌɍʴʵɼʶˢŚśŜŝŠšṠṡŞşȘșſẛȿƩƪŤťṪṫŢţȚțƾŦŧȾƫƬƭƮȶŬŭǓǔŮůǗǘǛǜǙǚǕǖŰűŨũŲųŪūȔȕȖȗƯưɄƜƱƲɅʷẂẃẀẁŴŵẄẅˣʸỲỳŶŷŸȲȳɎɏƳƴȜȝŹźŽžŻżƍƵƶȤȥɀƷʒǮǯƸƹƺƿǷƻƧƨƼƽƄƅɁɂˀʼˮʾˤʿˁǀǁǂǃΑαΆάΒβϐΓγΔδΕεϵΈέϜϝͶͷϚϛΖζͰͱΗηΉήΘθϑϴͺΙιΊίΪϊΐͿϳΚκϰϏϗΛλΜμΝνΞξΟοΌόΠπϖϺϻϞϟϘϙΡρϱϼΣςσϲϹͼϾͻϽͽϿΤτΥυϒΎύϓΫϋϔΰΦφϕΧχΨψΩωΏώϠϡͲͳϷϸϢϣϤϥϦϧϨϩϪϫϬϭϮϯАаⷶӐӑӒӓӘәӚӛӔӕБбⷠВвⷡГгⷢЃѓҐґҒғӺӻҔҕӶӷДдⷣԀԁꚀꚁЂђꙢꙣԂԃҘҙЕеⷷЀѐӖӗЁёЄєꙴЖжⷤӁӂӜӝԪԫꚄꚅҖҗЗзⷥӞӟꙀꙁԄԅԐԑꙂꙃЅѕꙄꙅӠӡꚈꚉԆԇꚂꚃИиꙵЍѝӤӥӢӣҊҋІіЇїꙶꙆꙇЙйЈјⷸꙈꙉКкⷦЌќҚқӃӄҠҡҞҟҜҝԞԟԚԛЛлⷧӅӆԮԯԒԓԠԡЉљꙤꙥԈԉԔԕМмⷨӍӎꙦꙧНнⷩԨԩӉӊҢңӇӈԢԣҤҥЊњԊԋОоⷪꙨꙩꙪꙫꙬꙭꙮꚘꚙꚚꚛӦӧӨөӪӫПпⷫԤԥҦҧҀҁРрⷬҎҏԖԗСсⷭⷵԌԍҪҫТтⷮꚌꚍԎԏҬҭꚊꚋЋћУуꙷЎўӰӱӲӳӮӯҮүҰұⷹꙊꙋѸѹФфꚞХхⷯӼӽӾӿҲҳҺһԦԧꚔꚕѠѡꙻѾѿꙌꙍѼѽѺѻЦцⷰꙠꙡꚎꚏҴҵꚐꚑЧчⷱӴӵԬԭꚒꚓҶҷӋӌҸҹꚆꚇҼҽҾҿЏџШшⷲꚖꚗЩщⷳꙎꙏꙿЪъꙸꚜꙐꙑЫыꙹӸӹЬьꙺꚝҌҍѢѣⷺꙒꙓЭэӬӭЮюⷻꙔꙕⷼꙖꙗЯяԘԙѤѥꚟѦѧⷽꙘꙙѪѫⷾꙚꙛѨѩꙜꙝѬѭⷿѮѯѰѱѲѳⷴѴѵѶѷꙞꙟҨҩԜԝӀӏ";

    public readonly char[] PuaCharCodes =
    {
      '\uE000', '\uE001', '\uE002', '\uE003', '\uE004', '\uE005', '\uE006', '\uE007', '\uE008', '\uE009', '\uE00A', '\uE00B', '\uE00C', '\uE00D', '\uE00E', '\uE00F', '\uE010', '\uE011', '\uE012', '\uE013', '\uE014', '\uE015', '\uE016', '\uE017', '\uE018', '\uE019', '\uE01A', '\uE01B', '\uE01C', '\uE01D', '\uE01E', '\uE01F', '\uE020', '\uE021', '\uE022', '\uE023', '\uE024', '\uE025', '\uE026', '\uE027', '\uE028', '\uE029', '\uE02A', '\uE02B', '\uE02C', '\uE02D', '\uE02E', '\uE02F', '\uE030', '\uE031', '\uE032', '\uE033', '\uE034', '\uE035', '\uE036', '\uE037', '\uE038', '\uE039', '\uE03A', '\uE03B', '\uE03C', '\uE03D', '\uE03E', '\uE03F', '\uE040', '\uE041', '\uE042', '\uE043', '\uE044', '\uE045', '\uE046', '\uE047', '\uE048', '\uE049', '\uE04A', '\uE04B', '\uE04C', '\uE04D', '\uE04E', '\uE04F', '\uE050', '\uE051', '\uE052', '\uE053', '\uE054', '\uE055', '\uE056', '\uE057', '\uE058', '\uE059', '\uE05A', '\uE05B', '\uE05C', '\uE05D', '\uE05E', '\uE05F', '\uE060', '\uE061', '\uE062', '\uE063', '\uE064', '\uE065', '\uE066', '\uE067', '\uE068', '\uE069', '\uE06A', '\uE06B', '\uE06C', '\uE06D', '\uE06E', '\uE06F', '\uE070', '\uE071', '\uE072', '\uE073', '\uE074', '\uE075', '\uE076', '\uE077', '\uE078', '\uE079', '\uE07A', '\uE07B', '\uE07C', '\uE07D', '\uE07E', '\uE07F', '\uE080', '\uE081', '\uE082', '\uE083', '\uE084', '\uE085', '\uE086', '\uE087', '\uE088', '\uE089', '\uE08A', '\uE08B', '\uE08C', '\uE08D', '\uE08E', '\uE08F', '\uE090', '\uE091', '\uE092', '\uE093', '\uE094', '\uE095', '\uE096', '\uE097', '\uE098', '\uE099', '\uE09A', '\uE09B', '\uE09C', '\uE09D', '\uE09E', '\uE09F', '\uE0A0', '\uE0A1', '\uE0A2', '\uE0A3', '\uE0A4', '\uE0A5', '\uE0A6', '\uE0A7', '\uE0A8', '\uE0A9', '\uE0AA', '\uE0AB', '\uE0AC', '\uE0AD', '\uE0AE', '\uE0AF', '\uE0B0', '\uE0B1', '\uE0B2', '\uE0B3', '\uE0B4', '\uE0B5', '\uE0B6', '\uE0B7', '\uE0B8', '\uE0B9', '\uE0BA', '\uE0BB', '\uE0BC', '\uE0BD', '\uE0BE', '\uE0BF', '\uE0C0', '\uE0C1', '\uE0C2', '\uE0C3', '\uE0C4', '\uE0C5', '\uE0C6', '\uE0C7', '\uE0C8', '\uE0C9', '\uE0CA', '\uE0CB', '\uE0CC', '\uE0CD', '\uE0CE', '\uE0CF', '\uE0D0', '\uE0D1', '\uE0D2', '\uE0D3', '\uE0D4', '\uE0D5', '\uE0D6', '\uE0D7', '\uE0D8', '\uE0D9', '\uE0DA', '\uE0DB', '\uE0DC', '\uE0DD', '\uE0DE', '\uE0DF', '\uE0E0', '\uE0E1', '\uE0E2', '\uE0E3', '\uE0E4', '\uE0E5', '\uE0E6', '\uE0E7', '\uE0E8', '\uE0E9', '\uE0EA', '\uE0EB', '\uE0EC', '\uE0ED', '\uE0EE', '\uE0EF', '\uE0F0', '\uE0F1', '\uE0F2', '\uE0F3', '\uE0F4', '\uE0F5', '\uE0F6', '\uE0F7', '\uE0F8', '\uE0F9', '\uE0FA', '\uE0FB', '\uE0FC', '\uE0FD', '\uE0FE', '\uE0FF', '\uE100', '\uE101', '\uE102', '\uE103', '\uE104', '\uE105', '\uE106', '\uE107', '\uE108', '\uE109', '\uE10A', '\uE10B', '\uE10C', '\uE10D', '\uE10E', '\uE10F', '\uE110', '\uE111', '\uE112', '\uE113', '\uE114', '\uE115', '\uE116', '\uE117', '\uE118', '\uE119', '\uE11A', '\uE11B', '\uE11C', '\uE11D', '\uE11E', '\uE11F', '\uE120', '\uE121', '\uE122', '\uE123', '\uE124', '\uE125', '\uE126', '\uE127', '\uE128', '\uE129', '\uE12A', '\uE12B', '\uE12C', '\uE12D', '\uE12E', '\uE12F', '\uE130', '\uE131', '\uE132', '\uE133', '\uE134', '\uE135', '\uE136', '\uE137', '\uE138', '\uE139', '\uE13A', '\uE13B', '\uE13C', '\uE13D', '\uE13E', '\uE13F', '\uE140', '\uE141', '\uE142', '\uE143', '\uE144', '\uE145', '\uE146', '\uE147', '\uE148', '\uE149', '\uE14A', '\uE14B', '\uE14C', '\uE14D', '\uE14E', '\uE14F', '\uE150', '\uE151', '\uE152', '\uE153', '\uE154', '\uE155', '\uE156', '\uE157', '\uE158', '\uE159', '\uE15A', '\uE15B', '\uE15C', '\uE15D', '\uE15E', '\uE15F', '\uE160', '\uE161', '\uE162', '\uE163', '\uE164', '\uE165', '\uE166', '\uE167', '\uE168', '\uE169', '\uE16A', '\uE16B', '\uE16C', '\uE16D', '\uE16E', '\uE16F', '\uE170', '\uE171', '\uE172', '\uE173', '\uE174', '\uE175', '\uE176', '\uE177', '\uE178', '\uE179', '\uE17A', '\uE17B', '\uE17C', '\uE17D', '\uE17E', '\uE17F', '\uE180', '\uE181', '\uE182', '\uE183', '\uE184', '\uE185', '\uE186', '\uE187', '\uE188', '\uE189', '\uE18A', '\uE18B', '\uE18C', '\uE18D', '\uE18E', '\uE18F', '\uE190', '\uE191', '\uE192', '\uE193', '\uE194', '\uE195', '\uE196', '\uE197', '\uE198', '\uE199', '\uE19A', '\uE19B', '\uE19C', '\uE19D', '\uE19E', '\uE19F', '\uE1A0', '\uE1A1', '\uE1A2', '\uE1A3', '\uE1A4', '\uE1A5', '\uE1A6', '\uE1A7', '\uE1A8', '\uE1A9', '\uE1AA', '\uE1AB', '\uE1AC', '\uE1AD', '\uE1AE', '\uE1AF', '\uE1B0', '\uE1B1', '\uE1B2', '\uE1B3', '\uE1B4', '\uE1B5', '\uE1B6', '\uE1B7', '\uE1B8', '\uE1B9', '\uE1BA', '\uE1BB', '\uE1BC', '\uE1BD', '\uE1BE', '\uE1BF', '\uE1C0', '\uE1C1', '\uE1C2', '\uE1C3', '\uE1C4', '\uE1C5', '\uE1C6', '\uE1C7', '\uE1C8', '\uE1C9', '\uE1CA', '\uE1CB', '\uE1CC', '\uE1CD', '\uE1CE', '\uE1CF', '\uE1D0', '\uE1D1', '\uE1D2', '\uE1D3', '\uE1D4', '\uE1D5', '\uE1D6', '\uE1D7', '\uE1D8', '\uE1D9', '\uE1DA', '\uE1DB', '\uE1DC', '\uE1DD', '\uE1DE', '\uE1DF', '\uE1E0', '\uE1E1', '\uE1E2', '\uE1E3', '\uE1E4', '\uE1E5', '\uE1E6', '\uE1E7', '\uE1E8', '\uE1E9', '\uE1EA', '\uE1EB', '\uE1EC', '\uE1ED', '\uE1EE', '\uE1EF', '\uE1F0', '\uE1F1', '\uE1F2', '\uE1F3', '\uE1F4', '\uE1F5', '\uE1F6', '\uE1F7', '\uE1F8', '\uE1F9', '\uE1FA', '\uE1FB', '\uE1FC', '\uE1FD', '\uE1FE', '\uE1FF', '\uE200', '\uE201', '\uE202', '\uE203', '\uE204', '\uE205', '\uE206', '\uE207', '\uE208', '\uE209', '\uE20A', '\uE20B', '\uE20C', '\uE20D', '\uE20E', '\uE20F', '\uE210', '\uE211', '\uE212', '\uE213', '\uE214', '\uE215', '\uE216', '\uE217', '\uE218', '\uE219', '\uE21A', '\uE21B', '\uE21C', '\uE21D', '\uE21E', '\uE21F', '\uE220', '\uE221', '\uE222', '\uE223', '\uE224', '\uE225', '\uE226', '\uE227', '\uE228', '\uE229', '\uE22A', '\uE22B', '\uE22C', '\uE22D', '\uE22E', '\uE22F', '\uE230', '\uE231', '\uE232', '\uE233', '\uE234', '\uE235', '\uE236', '\uE237', '\uE238', '\uE239', '\uE23A', '\uE23B', '\uE23C', '\uE23D', '\uE23E', '\uE23F', '\uE240', '\uE241', '\uE242', '\uE243', '\uE244', '\uE245', '\uE246', '\uE247', '\uE248', '\uE249', '\uE24A', '\uE24B', '\uE24C', '\uE24D', '\uE24E', '\uE24F', '\uE250', '\uE251', '\uE252', '\uE253', '\uE254', '\uE255', '\uE256', '\uE257', '\uE258', '\uE259', '\uE25A', '\uE25B', '\uE25C', '\uE25D', '\uE25E', '\uE25F', '\uE260', '\uE261', '\uE262', '\uE263', '\uE264', '\uE265', '\uE266', '\uE267', '\uE268', '\uE269', '\uE26A', '\uE26B', '\uE26C', '\uE26D', '\uE26E', '\uE26F', '\uE270', '\uE271', '\uE272', '\uE273', '\uE274', '\uE275', '\uE276', '\uE277', '\uE278', '\uE279', '\uE27A', '\uE27B', '\uE27C', '\uE27D', '\uE27E', '\uE27F', '\uE280', '\uE281', '\uE282', '\uE283', '\uE284', '\uE285', '\uE286', '\uE287', '\uE288', '\uE289', '\uE28A', '\uE28B', '\uE28C', '\uE28D', '\uE28E', '\uE28F', '\uE290', '\uE291', '\uE292', '\uE293', '\uE294', '\uE295', '\uE296', '\uE297', '\uE298', '\uE299', '\uE29A', '\uE29B', '\uE29C', '\uE29D', '\uE29E', '\uE29F', '\uE2A0', '\uE2A1', '\uE2A2', '\uE2A3', '\uE2A4', '\uE2A5', '\uE2A6', '\uE2A7', '\uE2A8', '\uE2A9', '\uE2AA', '\uE2AB', '\uE2AC', '\uE2AD', '\uE2AE', '\uE2AF', '\uE2B0', '\uE2B1', '\uE2B2', '\uE2B3', '\uE2B4', '\uE2B5', '\uE2B6', '\uE2B7', '\uE2B8', '\uE2B9', '\uE2BA', '\uE2BB', '\uE2BC', '\uE2BD', '\uE2BE', '\uE2BF', '\uE2C0', '\uE2C1', '\uE2C2', '\uE2C3', '\uE2C4', '\uE2C5', '\uE2C6', '\uE2C7', '\uE2C8', '\uE2C9', '\uE2CA', '\uE2CB', '\uE2CC', '\uE2CD', '\uE2CE', '\uE2CF', '\uE2D0', '\uE2D1', '\uE2D2', '\uE2D3', '\uE2D4', '\uE2D5', '\uE2D6', '\uE2D7', '\uE2D8', '\uE2D9', '\uE2DA', '\uE2DB', '\uE2DC', '\uE2DD', '\uE2DE', '\uE2DF', '\uE2E0', '\uE2E1', '\uE2E2', '\uE2E3', '\uE2E4', '\uE2E5', '\uE2E6', '\uE2E7', '\uE2E8', '\uE2E9', '\uE2EA', '\uE2EB', '\uE2EC', '\uE2ED', '\uE2EE', '\uE2EF', '\uE2F0', '\uE2F1', '\uE2F2', '\uE2F3', '\uE2F4', '\uE2F5', '\uE2F6', '\uE2F7', '\uE2F8', '\uE2F9', '\uE2FA', '\uE2FB', '\uE2FC', '\uE2FD', '\uE2FE', '\uE2FF', '\uE300', '\uE301', '\uE302', '\uE303', '\uE304', '\uE305', '\uE306', '\uE307', '\uE308', '\uE309', '\uE30A', '\uE30B', '\uE30C', '\uE30D', '\uE30E', '\uE30F', '\uE310', '\uE311', '\uE312', '\uE313', '\uE314', '\uE315', '\uE316', '\uE317', '\uE318', '\uE319', '\uE31A', '\uE31B', '\uE31C', '\uE31D', '\uE31E', '\uE31F', '\uE320', '\uE321', '\uE322', '\uE323', '\uE324', '\uE325', '\uE326', '\uE327', '\uE328', '\uE329', '\uE32A', '\uE32B', '\uE32C', '\uE32D', '\uE32E', '\uE32F', '\uE330', '\uE331', '\uE332', '\uE333', '\uE334', '\uE335', '\uE336', '\uE337', '\uE338', '\uE339', '\uE33A', '\uE33B', '\uE33C', '\uE33D', '\uE33E', '\uE33F', '\uE340', '\uE341', '\uE342', '\uE343', '\uE344', '\uE345', '\uE346', '\uE347', '\uE348', '\uE349', '\uE34A', '\uE34B', '\uE34C', '\uE34D', '\uE34E', '\uE34F', '\uE350', '\uE351', '\uE352', '\uE353', '\uE354', '\uE355', '\uE356', '\uE357', '\uE358', '\uE359', '\uE35A', '\uE35B', '\uE35C', '\uE35D', '\uE35E', '\uE35F', '\uE360', '\uE361', '\uE362', '\uE363', '\uE364', '\uE365', '\uE366', '\uE367', '\uE368', '\uE369', '\uE36A', '\uE36B', '\uE36C', '\uE36D', '\uE36E', '\uE36F', '\uE370', '\uE371', '\uE372', '\uE373', '\uE374', '\uE375', '\uE376', '\uE377', '\uE378', '\uE379', '\uE37A', '\uE37B', '\uE37C', '\uE37D', '\uE37E', '\uE37F', '\uE380', '\uE381', '\uE382', '\uE383', '\uE384', '\uE385', '\uE386', '\uE387', '\uE388', '\uE389', '\uE38A', '\uE38B', '\uE38C', '\uE38D', '\uE38E', '\uE38F', '\uE390', '\uE391', '\uE392', '\uE393', '\uE394', '\uE395', '\uE396', '\uE397', '\uE398', '\uE399', '\uE39A', '\uE39B', '\uE39C', '\uE39D', '\uE39E', '\uE39F', '\uE3A0', '\uE3A1', '\uE3A2', '\uE3A3', '\uE3A4', '\uE3A5', '\uE3A6', '\uE3A7', '\uE3A8', '\uE3A9', '\uE3AA', '\uE3AB', '\uE3AC', '\uE3AD', '\uE3AE', '\uE3AF', '\uE3B0', '\uE3B1', '\uE3B2', '\uE3B3', '\uE3B4', '\uE3B5', '\uE3B6', '\uE3B7', '\uE3B8', '\uE3B9', '\uE3BA', '\uE3BB', '\uE3BC', '\uE3BD', '\uE3BE', '\uE3BF', '\uE3C0', '\uE3C1', '\uE3C2', '\uE3C3', '\uE3C4', '\uE3C5', '\uE3C6', '\uE3C7', '\uE3C8', '\uE3C9', '\uE3CA', '\uE3CB', '\uE3CC', '\uE3CD', '\uE3CE', '\uE3CF', '\uE3D0', '\uE3D1', '\uE3D2', '\uE3D3', '\uE3D4', '\uE3D5', '\uE3D6', '\uE3D7', '\uE3D8', '\uE3D9', '\uE3DA', '\uE3DB', '\uE3DC', '\uE3DD', '\uE3DE', '\uE3DF', '\uE3E0', '\uE3E1', '\uE3E2', '\uE3E3', '\uE3E4', '\uE3E5', '\uE3E6', '\uE3E7', '\uE3E8', '\uE3E9', '\uE3EA', '\uE3EB', '\uE3EC', '\uE3ED', '\uE3EE', '\uE3EF', '\uE3F0', '\uE3F1', '\uE3F2', '\uE3F3', '\uE3F4', '\uE3F5', '\uE3F6', '\uE3F7', '\uE3F8', '\uE3F9', '\uE3FA', '\uE3FB', '\uE3FC', '\uE3FD', '\uE3FE', '\uE3FF', '\uE400', '\uE401', '\uE402', '\uE403', '\uE404', '\uE405', '\uE406', '\uE407', '\uE408', '\uE409', '\uE40A', '\uE40B', '\uE40C', '\uE40D', '\uE40E', '\uE40F',
    };

    public bool ConfigFontLoaded;
    public bool ConfigFontLoadFailed;
    public ImFontPtr ConfigUiFont;
    public readonly string LangComboItems = "Afrikaans; Afrikaans Shqip; Albanian   العَرَبِيَّة   Al'Arabiyyeẗ; Arabic Aragonés; Aragonese Հայերէն Hayerèn; Հայերեն Hayeren; Armenian Armãneashce; Armãneashti; Rrãmãneshti; Aromanian; Arumanian; Macedo-Romanian Azərbaycan Dili; آذربایجان دیلی; Азәрбајҹан Дили; Azerbaijani Euskara; Basque Беларуская Мова Belaruskaâ Mova; Belarusian Беларуская Мова Belaruskaâ Mova; Belarusian বাংলা Bāŋlā; Bengali ইমার ঠার/বিষ্ণুপ্রিয়া মণিপুরী Bishnupriya Manipuri Language; Bishnupriya Manipuri Bosanski; Bosnian Brezhoneg; Breton Български Език Bălgarski Ezik; Bulgarian မြန်မာစာ Mrãmācā; မြန်မာစကား Mrãmākā:; Burmese ; Cantonese Català,Valencià; Catalan; Valencian Sinugbuanong Binisayâ; Cebuano ភាសាខ្មែរ Phiəsaakhmær; Central Khmer Chichewa; Chinyanja; Chichewa; Chewa; Nyanja 中文 Zhōngwén; 汉语; 漢語 Hànyǔ; Chinese 中文 Zhōngwén; 汉语; 漢語 Hànyǔ; Chinese Corsu; Lingua Corsa; Corsican Hrvatski; Croatian Čeština; Český Jazyk; Czech Dansk; Danish Nederlands; Vlaams; Dutch; Flemish English; English English; English Esperanto; Esperanto Eesti Keel; Estonian Suomen Kieli; Finnish Français; French Gàidhlig; Gaelic; Scottish Gaelic Galego; Galician ᲥᲐᲠᲗᲣᲚᲘᲥართული Kharthuli; Georgian Deutsch; German Νέα Ελληνικά Néa Ellêniká; Greek, Modern (1453-) ગુજરાતી Gujarātī; Gujarati Kreyòl Ayisyen; Haitian; Haitian Creole Harshen Hausa; هَرْشَن; Hausa ʻōlelo Hawaiʻi; Hawaiian עברית 'Ivriyþ; Hebrew हिन्दी Hindī; Hindi Lus Hmoob; Lug Moob; Lol Hmongb; Hmong; Mong Magyar Nyelv; Hungarian Íslenska; Icelandic Asụsụ Igbo; Igbo Bahasa Indonesia; Indonesian Gaeilge; Irish Italiano; Lingua Italiana; Italian 日本語 Nihongo; Japanese ꦧꦱꦗꦮ  Basa Jawa; Javanese ಕನ್ನಡ Kannađa; Kannada Қазақ Тілі Qazaq Tili; Қазақша Qazaqşa; Kazakh Ikinyarwanda; Kinyarwanda Кыргызча Kırgızça; Кыргыз Тили Kırgız Tili; Kirghiz; Kyrgyz 한국어 Han'Gug'Ô; Korean Kurdî  کوردی; Kurdish ພາສາລາວ Phasalaw; Lao Lingua Latīna; Latin Latviešu Valoda; Latvian Lietuvių Kalba; Lithuanian ; Lombard Lëtzebuergesch; Luxembourgish; Letzeburgesch Македонски Јазик Makedonski Jazik; Macedonian ; Malagasy Bahasa Melayu; Malay മലയാളം Malayāļã; Malayalam Malti; Maltese Te Reo Māori; Maori मराठी Marāţhī; Marathi Монгол Хэл Mongol Xel; ᠮᠣᠩᠭᠣᠯ ᠬᠡᠯᠡ; Mongolian नेपाली भाषा Nepālī Bhāśā; Nepali Norsk; Norwegian Norsk Nynorsk; Norwegian Nynorsk; Nynorsk, Norwegian Occitan; Lenga D'Òc; Occitan (Post 1500) ଓଡ଼ିଆ; Oriya ਪੰਜਾਬੀ  پنجابی Pãjābī; Panjabi; Punjabi فارسی Fārsiy; Persian ; Piedmontese Język Polski; Polish Português; Portuguese پښتو Pax̌Tow; Pushto; Pashto Limba Română; Romanian; Moldavian; Moldovan Русский Язык Russkiĭ Âzık; Russian Gagana Faʻa Sāmoa; Samoan Српски  Srpski; Serbian ; Serbo-Croatian Chishona; Shona سنڌي  सिन्धी  ਸਿੰਧੀ; Sindhi සිංහල Sĩhala; Sinhala; Sinhalese Slovenčina; Slovenský Jazyk; Slovak Slovenski Jezik; Slovenščina; Slovenian Af Soomaali; Somali Sesotho [Southern]; Sotho, Southern Español; Castellano; Spanish; Castilian Basa Sunda; Sundanese Kiswahili; Swahili Svenska; Swedish Wikang Tagalog; Tagalog Тоҷикӣ Toçikī; Tajik தமிழ் Tamił; Tamil Татар Теле  Tatar Tele  تاتار; Tatar తెలుగు Telugu; Telugu ภาษาไทย Phasathay; Thai Türkçe; Turkish Türkmençe  Түркменче  تورکمن تیلی تورکمنچ; Türkmen Dili  Түркмен Дили; Turkmen ئۇيغۇرچە  ; ئۇيغۇر تىلى; Uighur; Uyghur Українська Мова; Українська; Ukrainian اُردُو Urduw; Urdu Oʻzbekcha  Ózbekça  Ўзбекча  ئوزبېچه; Oʻzbek Tili  Ўзбек Тили  ئوبېک تیلی; Uzbek Tiếng Việt; Vietnamese ; Volapük Winaray; Samareño; Lineyte-Samarnon; Binisayâ Nga Winaray; Binisayâ Nga Samar-Leyte; “Binisayâ Nga Waray”; Waray Cymraeg; Y Gymraeg; Welsh Frysk; Western Frisian Isixhosa; Xhosa ייִדיש; יידיש; אידיש Yidiš; Yiddish ";

    private string specialFontFileName = string.Empty;
    private string fontFileName = "NotoSans-Medium.ttf";
    private string scriptCharList = string.Empty;

    private void AdjustLanguageForFontBuild()
    {
      PluginLog.Debug("Inside AdjustLanguageForFontBuild method");
      var lang = this.LanguagesDictionary[this.configuration.Lang];
      this.specialFontFileName = lang.FontName;
      this.scriptCharList = lang.ExclusiveCharsToAdd;
    }

    private void LoadFont()
    {
      this.AdjustLanguageForFontBuild();

      var specialFontFilePath = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.specialFontFileName}";
      var fontFilePath = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}{this.fontFileName}";
      var dummyFontFilePath = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Regular.ttf";

      PluginLog.LogWarning("Inside LoadFont method");
      PluginLog.LogWarning($"Font file in DEBUG Mode: {specialFontFilePath}");

      this.FontLoaded = false;
      if (File.Exists(specialFontFilePath) || File.Exists(fontFilePath))
      {
        try
        {
          unsafe
          {
            var io = ImGui.GetIO();
            List<ushort> chars = new();

            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            builder.AddText(this.CharsToAddToAll);
            builder.AddText(this.scriptCharList);

            foreach (char c in this.PuaCharCodes)
            {
              builder.AddChar(c);
            }

            builder.BuildRanges(out ImVector ranges);

            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesDefault());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
            if (this.configuration.Lang is 16)
            {
              this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
            }

            if (this.configuration.Lang is 22 or 21)
            {
              this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseFull());
            }

            if (this.configuration.Lang is 56)
            {
              this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesKorean());
            }

            if (this.configuration.Lang is 50)
            {
              this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesJapanese());
            }

            if (this.configuration.Lang is 103)
            {
              this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesThai());
            }

            this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

            var addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
            chars.AddRange(addChars);

            chars.Add(0);

            var arr = chars.ToArray();

            var nativeConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            var fontConfig = new ImFontConfigPtr(nativeConfig)
            {
              OversampleH = 2,
              OversampleV = 2,
              MergeMode = true,
            };

            var fontConfig2 = new ImFontConfigPtr(nativeConfig)
            {
              OversampleH = 2,
              OversampleV = 2,
              MergeMode = true,
            };

            var fontConfig3 = new ImFontConfigPtr(nativeConfig)
            {
              OversampleH = 2,
              OversampleV = 2,
            };

            fixed (ushort* ptr = &arr[0])
            {
              if (specialFontFilePath != string.Empty)
              {
                ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath, this.configuration.FontSize,
                null);
                ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFilePath, this.configuration.FontSize,
                fontConfig2, new IntPtr(ptr));
                this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(specialFontFilePath, this.configuration.FontSize,
                  fontConfig, new IntPtr(ptr));
              }
              else
              {
                ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath, this.configuration.FontSize,
                  fontConfig2, new IntPtr(ptr));
                this.UiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFilePath, this.configuration.FontSize,
                  fontConfig, new IntPtr(ptr));
              }
            }

#if DEBUG
            PluginLog.Debug($"UiFont Data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
            this.FontLoaded = true;
#if DEBUG
            PluginLog.Debug($"Font loaded? {this.FontLoaded}");
#endif
          }
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Special Font failed to load. {specialFontFilePath}");
          PluginLog.Log(ex.ToString());
          this.FontLoadFailed = true;
        }
      }
      else
      {
        PluginLog.Log($"Special Font doesn't exist. {specialFontFilePath}");
        this.FontLoadFailed = true;
      }
    }

    private void LoadConfigFont()
    {
#if DEBUG
      PluginLog.LogVerbose("Inside LoadConfigFont method");
      var fontFile = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
      var dummyFontFilePath = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}OpenSans-Medium.ttf";

      PluginLog.LogVerbose($"Font file in DEBUG Mode: {fontFile}");
#else
      // PluginLog.LogVerbose("Inside LoadConfigFont method");
      var fontFile = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}NotoSans-Medium-Custom2.otf";
      var dummyFontFilePath = $@"{this.pluginInterface.AssemblyLocation.DirectoryName}{Path.DirectorySeparatorChar}Font{Path.DirectorySeparatorChar}OpenSans-Medium.ttf";

      // PluginLog.LogVerbose($"Font file in PROD Mode: {fontFile}");
#endif
      this.ConfigFontLoaded = false;
      if (File.Exists(fontFile))
      {
        try
        {
          unsafe
          {
            var io = ImGui.GetIO();
            List<ushort> chars = new();

            var builder = new ImFontGlyphRangesBuilderPtr(ImGuiNative.ImFontGlyphRangesBuilder_ImFontGlyphRangesBuilder());
            builder.AddText(this.CharsToAddToAll);
            builder.AddText(this.LangComboItems);
            foreach (char c in this.PuaCharCodes)
            {
              builder.AddChar(c);
            }
            builder.BuildRanges(out ImVector ranges);

            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesDefault());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesVietnamese());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesCyrillic());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesThai());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesJapanese());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesKorean());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseFull());
            this.AddCharsFromIntPtr(chars, (ushort*)io.Fonts.GetGlyphRangesChineseSimplifiedCommon());
            this.AddCharsFromIntPtr(chars, (ushort*)ranges.Data);

            var addChars = string.Join(string.Empty, chars.Select(c => new string((char)c, 2))).Select(c => (ushort)c).ToArray();
            chars.AddRange(addChars);

            chars.Add(0);

            var arr = chars.ToArray();

            var nativeConfig = ImGuiNative.ImFontConfig_ImFontConfig();
            var fontConfig = new ImFontConfigPtr(nativeConfig)
            {
              OversampleH = 2,
              OversampleV = 2,
              MergeMode = true,
            };
            var fontConfig2 = new ImFontConfigPtr(nativeConfig)
            {
              OversampleH = 2,
              OversampleV = 2,
            };

            fixed (ushort* ptr = &arr[0])
            {
              ImGui.GetIO().Fonts.AddFontFromFileTTF(dummyFontFilePath, 17.0f, null, new IntPtr(ptr));
              this.ConfigUiFont = ImGui.GetIO().Fonts.AddFontFromFileTTF(fontFile, 17.0f,
                  fontConfig, new IntPtr(ptr));
            }

#if DEBUG
            PluginLog.Debug($"ConfigUiFont data size: {ImGui.GetIO().Fonts.Fonts.Size}");
#endif
            this.ConfigFontLoaded = true;
#if DEBUG
            PluginLog.Debug($"Config Font loaded? {this.ConfigFontLoaded}");
#endif
            fontConfig.Destroy();
          }
        }
        catch (Exception ex)
        {
          PluginLog.Log($"Config Font failed to load. {fontFile}");
          PluginLog.Log(ex.ToString());
          this.ConfigFontLoadFailed = true;
        }
      }
      else
      {
        PluginLog.Log($"Config Font doesn't exist. {fontFile}");
        this.ConfigFontLoadFailed = true;
      }
    }

    private unsafe void AddCharsFromIntPtr(List<ushort> chars, ushort* ptr)
    {
      while (*ptr != 0)
      {
        chars.Add(*ptr);
        ptr++;
      }
    }
  }
}