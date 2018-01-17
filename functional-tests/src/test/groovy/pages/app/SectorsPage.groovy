package pages.app

import org.openqa.selenium.By

import geb.Page

class SectorsPage extends Page {

	static at = { driver.findElement(By.xpath("//*[@id='main-content']/div[1]/div[2]/h3")).getText() == "News by Sector" }
    static url = "/sectors"	
}
