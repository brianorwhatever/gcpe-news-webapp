package pages.app

import org.openqa.selenium.By

import geb.Page

class ConnectPage extends Page {

	static at = { driver.findElement(By.xpath("//*[@id='main-content']/div[1]/div[2]/h3")).getText() == "Connect" }
	
    //static at = { title=="Connect | BC Gov News" }
    static url = "/connect"
}
